// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.SearchService.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Timers;

namespace NuGet.Services.SearchService
{
    public class Startup
    {
        public const string EnvironmentVariablePrefix = "APPSETTING_";
        private const string ControllerSuffix = "Controller";
        private const string ConfigurationSectionName = "SearchService";

        public Startup(IConfiguration configuration)
        {
            Configuration = (IConfigurationRoot)configuration;
        }

        public IConfigurationRoot Configuration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            var refreshableConfig = GetSecretInjectedConfiguration(Configuration);
            Configuration = refreshableConfig.Root;
            services.AddSingleton(refreshableConfig.SecretReaderFactory);

            services
                .AddControllers(o =>
                {
                    o.SuppressAsyncSuffixInActionNames = false;
                    o.Filters.Add<ApiExceptionFilterAttribute>();
                })
                .AddNewtonsoftJson(o =>
                {
                    o.SerializerSettings.ContractResolver = new DefaultContractResolver();
                    o.SerializerSettings.Converters.Add(new StringEnumConverter());
                    o.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                });

            services.Configure<AzureSearchConfiguration>(Configuration.GetSection(ConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(Configuration.GetSection(ConfigurationSectionName));

            services.AddApplicationInsightsTelemetry(Configuration.GetValue<string>("ApplicationInsights_InstrumentationKey"));
            services.AddSingleton<ITelemetryInitializer>(new KnownOperationNameEnricher(new[]
            {
                GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.AutocompleteAsync)),
                GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.IndexAsync)),
                GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.GetStatusAsync)),
                GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.V2SearchAsync)),
                GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.V3SearchAsync)),
            }));
            services.AddApplicationInsightsTelemetryProcessor<SearchRequestTelemetryProcessor>();
            services.AddSingleton<TelemetryClient>();
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();

            services.AddHostedService<AuxiliaryFileReloaderBackgroundService>();
            services.AddHostedService<SecretRefresherBackgroundService>();

            services.AddAzureSearch(new Dictionary<string, string>());

            // The maximum SNAT ports on Azure App Service is 128:
            // https://docs.microsoft.com/en-us/azure/app-service/troubleshoot-intermittent-outbound-connection-errors#cause
            ServicePointManager.DefaultConnectionLimit = 128;
            services
                .AddSingleton<HttpClientHandler>(s => new HttpClientHandler
                {
                    MaxConnectionsPerServer = 128,
                });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterAssemblyModules(typeof(Startup).Assembly);
            builder.AddAzureSearch();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors(cors => cors
                .AllowAnyOrigin()
                .WithHeaders("Content-Type", "If-Match", "If-Modified-Since", "If-None-Match", "If-Unmodified-Since", "Accept-Encoding")
                .WithMethods("GET", "HEAD", "OPTIONS")
                .WithExposedHeaders("Content-Type", "Content-Length", "Last-Modified", "Transfer-Encoding", "ETag", "Date", "Vary", "Server", "X-Hit", "X-CorrelationId"));

            app.UseHsts();

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static RefreshableConfiguration GetSecretInjectedConfiguration(IConfigurationRoot uninjectedConfiguration)
        {
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

            // Reload the configuration with secret injection enabled. This is used by the application.
            var injectedBuilder = new ConfigurationBuilder()
                .AddInjectedJsonFile("appsettings.json", secretInjector)
                .AddInjectedJsonFile("appsettings.Development.json", secretInjector)
                .AddInjectedEnvironmentVariables(EnvironmentVariablePrefix, secretInjector);
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

        private static string GetControllerName<T>() where T : ControllerBase
        {
            var typeName = typeof(T).Name;
            if (typeName.EndsWith(ControllerSuffix, StringComparison.Ordinal))
            {
                return typeName.Substring(0, typeName.Length - ControllerSuffix.Length);
            }

            throw new ArgumentException($"The controller type name must end with '{ControllerSuffix}'.");
        }

        private static string GetOperationName<T>(HttpMethod verb, string actionName) where T : ControllerBase
        {
            return $"{verb} {GetControllerName<T>()}/{actionName}";
        }

        private class RefreshableConfiguration
        {
            public IRefreshableSecretReaderFactory SecretReaderFactory { get; set; }
            public IConfigurationRoot Root { get; set; }
        }
    }
}
