// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Monitoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class Job : JsonConfigurationJob
    {
        private const string MonitorConfigurationSectionName = "MonitorConfiguration";
        private const int MAX_CATALOG_RETRY_COUNT = 5;

        private IPackageLagTelemetryService _telemetryService;
        private ISearchServiceClient _searchServiceClient;
        private ICatalogClient _catalogClient;
        private PackageLagMonitorConfiguration _configuration;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _configuration = _serviceProvider.GetService<PackageLagMonitorConfiguration>();
            _catalogClient = _serviceProvider.GetService<ICatalogClient>();
            _searchServiceClient = _serviceProvider.GetService<ISearchServiceClient>();
            _telemetryService = _serviceProvider.GetService<IPackageLagTelemetryService>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<PackageLagMonitorConfiguration>(configurationRoot.GetSection(MonitorConfigurationSectionName));

            services.AddSingleton(p => new HttpClient());
            services.AddSingleton<IHttpClientWrapper>(p => new HttpClientWrapper(p.GetService<HttpClient>()));
            services.AddTransient<IPackageLagTelemetryService, PackageLagTelemetryService>();
            services.AddSingleton(new TelemetryClient(ApplicationInsightsConfiguration.TelemetryConfiguration));
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient(p => p.GetService<IOptionsSnapshot<PackageLagMonitorConfiguration>>().Value);
            services.AddSingleton<ISimpleHttpClient, SimpleHttpClient>();
            services.AddSingleton<ICatalogClient, CatalogClient>();
            services.AddTransient<ISearchServiceClient, SearchServiceClient>();
        }

        public async override Task Run()
        {
            var token = new CancellationToken();
            try
            {
                var regionInformations = _configuration.RegionInformations;
                var instances = new List<Instance>();

                foreach (var regionInformation in regionInformations)
                {
                    instances.AddRange(_searchServiceClient.GetSearchEndpoints(regionInformation));
                }

                var maxCommit = DateTimeOffset.MinValue;

                foreach (Instance instance in instances)
                {
                    try
                    {
                        var commitDateTime = await _searchServiceClient.GetCommitDateTimeAsync(instance, token);

                        maxCommit = commitDateTime > maxCommit ? commitDateTime : maxCommit;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("An exception was encountered so no HTTP response was returned. {Exception}", e);
                    }
                }

                if (maxCommit == DateTimeOffset.MinValue)
                {
                    Logger.LogError("Failed to retrieve a proper starting commit. Abandoning the current run.");
                    return;
                }
                
                var catalogLeafProcessor = new PackageLagCatalogLeafProcessor(instances, _searchServiceClient, _telemetryService, LoggerFactory.CreateLogger<PackageLagCatalogLeafProcessor>());
                if (_configuration.RetryLimit > 0)
                {
                    catalogLeafProcessor.RetryLimit = _configuration.RetryLimit;
                }

                if(_configuration.WaitBetweenRetrySeconds > 0)
                {
                    catalogLeafProcessor.WaitBetweenPolls = TimeSpan.FromSeconds(_configuration.WaitBetweenRetrySeconds);
                }

                var settings = new CatalogProcessorSettings
                {
                    ServiceIndexUrl = _configuration.ServiceIndexUrl,
                    DefaultMinCommitTimestamp = maxCommit,
                    ExcludeRedundantLeaves = false
                };

                var start = new FileCursor("cursor.json", LoggerFactory.CreateLogger<FileCursor>());
                await start.SetAsync(maxCommit.AddTicks(1));

                var catalogProcessor = new CatalogProcessor(start, _catalogClient, catalogLeafProcessor, settings, LoggerFactory.CreateLogger<CatalogProcessor>());

                bool success;
                int retryCount = 0;
                do
                {
                    success = await catalogProcessor.ProcessAsync();
                    if (!success || !await catalogLeafProcessor.WaitForProcessing())
                    {
                        retryCount++;
                        Logger.LogError("Processing the catalog leafs failed. Retry Count {CatalogProcessRetryCount}", retryCount);
                    }
                }
                while (!success && retryCount < MAX_CATALOG_RETRY_COUNT);

                return;
            }
            catch (Exception e)
            {
                Logger.LogError("Exception Occured. {Exception}", e);
                return;
            }
        }
    }
}
