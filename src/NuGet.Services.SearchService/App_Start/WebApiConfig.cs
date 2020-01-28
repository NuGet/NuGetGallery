// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.Cors;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Integration.WebApi;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.Web;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.SearchService.Controllers;

namespace NuGet.Services.SearchService
{
    public static class WebApiConfig
    {
        private const string ControllerSuffix = "Controller";
        private const string ConfigurationSectionName = "SearchService";

        public static void Register(HttpConfiguration config)
        {
            config.Filters.Add(new ApiExceptionFilterAttribute());

            config.Formatters.Remove(config.Formatters.XmlFormatter);
            SetSerializerSettings(config.Formatters.JsonFormatter.SerializerSettings);

            var dependencyResolver = GetDependencyResolver(config);
            config.DependencyResolver = dependencyResolver;

            config.EnableCors(new EnableCorsAttribute(
                origins: "*",
                headers: "Content-Type,If-Match,If-Modified-Since,If-None-Match,If-Unmodified-Since,Accept-Encoding",
                methods: "GET,HEAD,OPTIONS",
                exposedHeaders: "Content-Type,Content-Length,Last-Modified,Transfer-Encoding,ETag,Date,Vary,Server,X-Hit,X-CorrelationId")
            {
                PreflightMaxAge = 3600
            });

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "Index",
                routeTemplate: "",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.IndexAsync),
                });

            config.Routes.MapHttpRoute(
                name: "GetStatus",
                routeTemplate: "search/diag",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.GetStatusAsync),
                });

            config.Routes.MapHttpRoute(
                name: "V2Search",
                routeTemplate: "search/query",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.V2SearchAsync),
                });

            config.Routes.MapHttpRoute(
                name: "V3Search",
                routeTemplate: "query",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.V3SearchAsync),
                });

            config.Routes.MapHttpRoute(
                name: "Autocomplete",
                routeTemplate: "autocomplete",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.AutocompleteAsync),
                });

            config.EnsureInitialized();

            HostingEnvironment.QueueBackgroundWorkItem(token => ReloadAuxiliaryFilesAsync(dependencyResolver.Container, token));
            HostingEnvironment.QueueBackgroundWorkItem(token => RefreshSecretsAsync(dependencyResolver.Container, token));
        }

        public static void SetSerializerSettings(JsonSerializerSettings settings)
        {
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.Converters.Add(new StringEnumConverter());
        }

        private static async Task ReloadAuxiliaryFilesAsync(ILifetimeScope serviceProvider, CancellationToken token)
        {
            var loader = serviceProvider.Resolve<IAuxiliaryFileReloader>();
            await loader.ReloadContinuouslyAsync(token);
        }

        private static async Task RefreshSecretsAsync(ILifetimeScope serviceProvider, CancellationToken token)
        {
            var loader = serviceProvider.Resolve<ISecretRefresher>();
            await loader.RefreshContinuouslyAsync(token);
        }

        private static AutofacWebApiDependencyResolver GetDependencyResolver(HttpConfiguration config)
        {
            var configuration = GetConfiguration();

            var globalTelemetryDimensions = new Dictionary<string, string>();
            var applicationInsightsConfiguration = InitializeApplicationInsights(configuration);

            var services = new ServiceCollection();
            services.AddSingleton(configuration.SecretReaderFactory);
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.Configure<AzureSearchConfiguration>(configuration.Root.GetSection(ConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(configuration.Root.GetSection(ConfigurationSectionName));
            services.AddAzureSearch(globalTelemetryDimensions);
            services.AddSingleton(new TelemetryClient(applicationInsightsConfiguration.TelemetryConfiguration));
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();

            var builder = new ContainerBuilder();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            builder.RegisterWebApiFilterProvider(config);
            builder.RegisterWebApiModelBinderProvider();
            builder.Populate(services);
            builder.AddAzureSearch();

            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: false);
            var loggerFactory = LoggingSetup.CreateLoggerFactory(
                loggerConfiguration,
                telemetryConfiguration: applicationInsightsConfiguration.TelemetryConfiguration);

            builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();
            builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));

            var container = builder.Build();
            return new AutofacWebApiDependencyResolver(container);
        }

        private static ApplicationInsightsConfiguration InitializeApplicationInsights(RefreshableConfiguration configuration)
        {
            var instrumentationKey = configuration.Root.GetValue<string>("ApplicationInsights_InstrumentationKey");
            var heartbeatIntervalSeconds = configuration.Root.GetValue("ApplicationInsights_HeartbeatIntervalSeconds", 60);

            var applicationInsightsConfiguration = ApplicationInsights.Initialize(
                    instrumentationKey,
                    TimeSpan.FromSeconds(heartbeatIntervalSeconds));

            applicationInsightsConfiguration.TelemetryConfiguration.TelemetryInitializers.Add(new AzureWebAppTelemetryInitializer());

            RegisterApplicationInsightsTelemetryModules(applicationInsightsConfiguration.TelemetryConfiguration);

            return applicationInsightsConfiguration;
        }

        private static void RegisterApplicationInsightsTelemetryModules(TelemetryConfiguration configuration)
        {
            RegisterApplicationInsightsTelemetryModule(
                new AppServicesHeartbeatTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new AzureInstanceMetadataTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new DeveloperModeWithDebuggerAttachedTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new UnhandledExceptionTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new UnobservedExceptionTelemetryModule(),
                configuration);

            var requestTrackingModule = new RequestTrackingTelemetryModule();
            requestTrackingModule.Handlers.Add("Microsoft.VisualStudio.Web.PageInspector.Runtime.Tracing.RequestDataHttpHandler");
            requestTrackingModule.Handlers.Add("System.Web.StaticFileHandler");
            requestTrackingModule.Handlers.Add("System.Web.Handlers.AssemblyResourceLoader");
            requestTrackingModule.Handlers.Add("System.Web.Optimization.BundleHandler");
            requestTrackingModule.Handlers.Add("System.Web.Script.Services.ScriptHandlerFactory");
            requestTrackingModule.Handlers.Add("System.Web.Handlers.TraceHandler");
            requestTrackingModule.Handlers.Add("System.Web.Services.Discovery.DiscoveryRequestHandler");
            requestTrackingModule.Handlers.Add("System.Web.HttpDebugHandler");
            RegisterApplicationInsightsTelemetryModule(
                requestTrackingModule,
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new ExceptionTrackingTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new AspNetDiagnosticTelemetryModule(),
                configuration);

            var dependencyTrackingModule = new DependencyTrackingTelemetryModule();
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
            dependencyTrackingModule.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
            dependencyTrackingModule.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
            RegisterApplicationInsightsTelemetryModule(
                dependencyTrackingModule,
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new PerformanceCollectorModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new QuickPulseTelemetryModule(),
                configuration);
        }

        private static void RegisterApplicationInsightsTelemetryModule(ITelemetryModule telemetryModule, TelemetryConfiguration configuration)
        {
            var existingModule = TelemetryModules.Instance.Modules.SingleOrDefault(m => m.GetType().Equals(telemetryModule.GetType()));
            if (existingModule != null)
            {
                TelemetryModules.Instance.Modules.Remove(existingModule);
            }

            telemetryModule.Initialize(configuration);

            TelemetryModules.Instance.Modules.Add(telemetryModule);
        }

        private static RefreshableConfiguration GetConfiguration()
        {
            const string prefix = "APPSETTING_";
            var jsonFile = Path.Combine(HostingEnvironment.MapPath("~/"), @"Settings\local.json");

            // Load the configuration without injection. This allows us to read KeyVault configuration.
            var uninjectedBuilder = new ConfigurationBuilder()
                .AddJsonFile(jsonFile) // The JSON file is useful for local development.
                .AddEnvironmentVariables(prefix); // Environment variables take precedence.
            var uninjectedConfiguration = uninjectedBuilder.Build();

            // Initialize KeyVault integration.
            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var refreshSecretReaderSettings = new RefreshableSecretReaderSettings();
            var refreshingSecretReaderFactory = new RefreshableSecretReaderFactory(secretReaderFactory, refreshSecretReaderSettings);
            var secretReader = refreshingSecretReaderFactory.CreateSecretReader();
            var secretInjector = refreshingSecretReaderFactory.CreateSecretInjector(secretReader);

            // Attempt to inject secrets into all of the configuration strings.
            foreach (var pair in uninjectedConfiguration.AsEnumerable())
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    // We can synchronously wait here because we are outside of the request context. It's not great
                    // but we need to fetch the initial secrets for the cache before activating any controllers or
                    // asking DI for configuration.
                    secretInjector.InjectAsync(pair.Value).Wait();
                }
            }

            // Reload the configuration with secret injection enabled. This is was is used by the application.
            var injectedBuilder = new ConfigurationBuilder()
                .AddInjectedJsonFile(jsonFile, secretInjector)
                .AddInjectedEnvironmentVariables(prefix, secretInjector);
            var injectedConfiguration = injectedBuilder.Build();

            // Now disable all secrets loads from a non-refresh path. Refresh will be called periodically from a
            // background thread. Foreground (request) threads MUST use the cache otherwise there will be a deadlock.
            refreshSecretReaderSettings.BlockUncachedReads = true;

            return new RefreshableConfiguration
            {
                SecretReaderFactory = refreshingSecretReaderFactory,
                Root = injectedConfiguration,
            };
        }

        private static string GetControllerName<T>() where T : ApiController
        {
            var typeName = typeof(T).Name;
            if (typeName.EndsWith(ControllerSuffix, StringComparison.Ordinal))
            {
                return typeName.Substring(0, typeName.Length - ControllerSuffix.Length);
            }

            throw new ArgumentException($"The controller type name must end with '{ControllerSuffix}'.");
        }

        private class RefreshableConfiguration
        {
            public IRefreshableSecretReaderFactory SecretReaderFactory { get; set; }
            public IConfigurationRoot Root { get; set; }
        }
    }
}