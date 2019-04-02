// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Integration.WebApi;
using Microsoft.ApplicationInsights.Extensibility;
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
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        public static void Register(HttpConfiguration config)
        {
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            config.Formatters.JsonFormatter.SerializerSettings.Converters.Add(new StringEnumConverter());

            var dependencyResolver = GetDependencyResolver(config);
            config.DependencyResolver = dependencyResolver;

            config.MapHttpAttributeRoutes();

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

            HostingEnvironment.QueueBackgroundWorkItem(token => ReloadAuxiliaryFilesAsync(dependencyResolver, token));
        }

        private static async Task ReloadAuxiliaryFilesAsync(IDependencyResolver dependencyResolver, CancellationToken token)
        {
            var loader = (IAuxiliaryFileReloader)dependencyResolver.GetService(typeof(IAuxiliaryFileReloader));
            await loader.ReloadContinuouslyAsync(token);
        }

        private static IDependencyResolver GetDependencyResolver(HttpConfiguration config)
        {
            var configurationRoot = GetConfigurationRoot();

            var instrumentationKey = configurationRoot.GetValue<string>("ApplicationInsights_InstrumentationKey");
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;
            }

            var services = new ServiceCollection();
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.AddAzureSearch();

            var builder = new ContainerBuilder();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            builder.RegisterWebApiFilterProvider(config);
            builder.RegisterWebApiModelBinderProvider();
            builder.Populate(services);
            builder.AddAzureSearch();

            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: false);
            var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
            builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();
            builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));

            var container = builder.Build();
            return new AutofacWebApiDependencyResolver(container);
        }

        private static IConfigurationRoot GetConfigurationRoot()
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
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretReader = cachingSecretReaderFactory.CreateSecretReader();
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(secretReader);

            // Reload the configuration with secret injection enabled. This is was is used by the application.
            var injectedBuilder = new ConfigurationBuilder()
                .AddInjectedJsonFile(jsonFile, secretInjector)
                .AddInjectedEnvironmentVariables(prefix, secretInjector);
            var injectedConfiguration = injectedBuilder.Build();

            return injectedConfiguration;
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
    }
}