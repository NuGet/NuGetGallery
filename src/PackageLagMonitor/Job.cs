// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Monitoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureManagement;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";

        private const string AzureManagementSectionName = "AzureManagement";
        private const string MonitorConfigurationSectionName = "MonitorConfiguration";
        private const int MAX_CATALOG_RETRY_COUNT = 5;

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private IAzureManagementAPIWrapper _azureManagementApiWrapper;
        private IPackageLagTelemetryService _telemetryService;
        private IHttpClientWrapper _httpClient;
        private ISearchServiceClient _searchServiceClient;
        private ICatalogClient _catalogClient;
        private IServiceProvider _serviceProvider;
        private PackageLagMonitorConfiguration _configuration;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename));

            _configuration = _serviceProvider.GetService<PackageLagMonitorConfiguration>();
            _azureManagementApiWrapper = _serviceProvider.GetService<IAzureManagementAPIWrapper>();
            _catalogClient = _serviceProvider.GetService<ICatalogClient>();
            _httpClient = _serviceProvider.GetService<IHttpClientWrapper>();
            _searchServiceClient = _serviceProvider.GetService<ISearchServiceClient>();

            _telemetryService = _serviceProvider.GetService<IPackageLagTelemetryService>();
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: false);

            var uninjectedConfiguration = builder.Build();

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();
            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<PackageLagMonitorConfiguration>(configurationRoot.GetSection(MonitorConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(configurationRoot.GetSection(MonitorConfigurationSectionName));
            services.Configure<AzureManagementAPIWrapperConfiguration>(configurationRoot.GetSection(AzureManagementSectionName));

            services.AddSingleton(p =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) =>
                    {
                        if (policyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch || policyErrors == System.Net.Security.SslPolicyErrors.None)
                        {
                            return true;
                        }

                        return false;
                    };
                return handler;
            });

            services.AddSingleton(p => new HttpClient(p.GetService<HttpClientHandler>()));
            services.AddSingleton<IHttpClientWrapper>(p => new HttpClientWrapper(p.GetService<HttpClient>()));
            services.AddTransient<IPackageLagTelemetryService, PackageLagTelemetryService>();
            services.AddSingleton(new TelemetryClient(ApplicationInsightsConfiguration.TelemetryConfiguration));
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<IAzureManagementAPIWrapperConfiguration>(p => p.GetService<IOptionsSnapshot<AzureManagementAPIWrapperConfiguration>>().Value);
            services.AddTransient<PackageLagMonitorConfiguration>(p => p.GetService<IOptionsSnapshot<PackageLagMonitorConfiguration>>().Value);
            services.AddSingleton<ICatalogClient, CatalogClient>();
            services.AddSingleton<IAzureManagementAPIWrapper, AzureManagementAPIWrapper>();
            services.AddTransient<ISearchServiceClient, SearchServiceClient>();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            return new AutofacServiceProvider(containerBuilder.Build());
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
                    instances.AddRange(await _searchServiceClient.GetSearchEndpointsAsync(regionInformation, token));
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
