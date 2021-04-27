// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PackageManagement.Search.Web;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.Logging;
using NuGet.Services.SearchService.Controllers;

namespace NuGet.Services.SearchService
{
    public class Startup
    {
        private const string ConfigurationSectionName = "SearchService";

        public Startup(IConfiguration configuration)
        {
            Configuration = (IConfigurationRoot)configuration;
        }

        public IConfigurationRoot Configuration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            var refreshableConfig = StartupHelper.GetSecretInjectedConfiguration(Configuration);
            Configuration = refreshableConfig.Root;
            services.AddSingleton(refreshableConfig.SecretReaderFactory);

            services
                .AddControllers(o =>
                {
                    o.SuppressAsyncSuffixInActionNames = false;
                    o.Filters.Add<ApiExceptionFilterAttribute>();
                })
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.PropertyNamingPolicy = null;
                    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    o.JsonSerializerOptions.Converters.Add(new TimeSpanConverter());
                    o.JsonSerializerOptions.IgnoreNullValues = true;
                });

            services.Configure<AzureSearchConfiguration>(Configuration.GetSection(ConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(Configuration.GetSection(ConfigurationSectionName));

            services.AddApplicationInsightsTelemetry(o =>
            {
                o.InstrumentationKey = Configuration.GetValue<string>("ApplicationInsights_InstrumentationKey");
                o.EnableAdaptiveSampling = false;
            });
            services.AddSingleton<ITelemetryInitializer>(new KnownOperationNameEnricher(new[]
            {
                StartupHelper.GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.AutocompleteAsync)),
                StartupHelper.GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.IndexAsync)),
                StartupHelper.GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.GetStatusAsync)),
                StartupHelper.GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.V2SearchAsync)),
                StartupHelper.GetOperationName<SearchController>(HttpMethod.Get, nameof(SearchController.V3SearchAsync)),
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
            StartupHelper.Configure(app, env);

            app.UseCors(cors => cors
                .AllowAnyOrigin()
                .WithHeaders("Content-Type", "If-Match", "If-Modified-Since", "If-None-Match", "If-Unmodified-Since", "Accept-Encoding")
                .WithMethods("GET", "HEAD", "OPTIONS")
                .WithExposedHeaders("Content-Type", "Content-Length", "Last-Modified", "Transfer-Encoding", "ETag", "Date", "Vary", "Server", "X-Hit", "X-CorrelationId"));
        }
    }
}
