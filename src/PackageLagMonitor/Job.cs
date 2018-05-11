// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
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
using Newtonsoft.Json;
using NuGet.Jobs.Montoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureManagement;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Montoring.PackageLag
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";

        private const string AzureManagementSectionName = "AzureManagement";
        private const string MonitorConfigurationSectionName = "MonitorConfiguration";

        /// <summary>
        /// To be used for <see cref="IAzureManagementAPIWrapper"/> request
        /// </summary>
        private const string ProductionSlot = "production";
        private const int MAX_CATALOG_RETRY_COUNT = 5;

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private IAzureManagementAPIWrapper _azureManagementApiWrapper;
        private IPackageLagTelemetryService _telemetryService;
        private HttpClient _httpClient;
        private ICatalogClient _catalogClient;
        private IServiceProvider _serviceProvider;
        private PackageLagMonitorConfiguration _configuration;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename));

            _configuration = _serviceProvider.GetService<PackageLagMonitorConfiguration>();
            _azureManagementApiWrapper = _serviceProvider.GetService<AzureManagementAPIWrapper>();
            _catalogClient = _serviceProvider.GetService<CatalogClient>();
            _httpClient = _serviceProvider.GetService<HttpClient>();

            _telemetryService = _serviceProvider.GetService<IPackageLagTelemetryService>();
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

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
            services.AddTransient<IPackageLagTelemetryService, PackageLagTelemetryService>();
            services.AddSingleton(new TelemetryClient());
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<IAzureManagementAPIWrapperConfiguration>(p => p.GetService<IOptionsSnapshot<AzureManagementAPIWrapperConfiguration>>().Value);
            services.AddTransient<PackageLagMonitorConfiguration>(p => p.GetService<IOptionsSnapshot<PackageLagMonitorConfiguration>>().Value);
            services.AddSingleton<CatalogClient>();
            services.AddSingleton<AzureManagementAPIWrapper>();
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
                var instances = await GetSearchEndpointsAsync(token);

                var maxCommit = DateTimeOffset.MinValue;

                foreach (Instance instance in instances)
                {
                    try
                    {
                        using (var diagResponse = await _httpClient.GetAsync(
                            instance.DiagUrl,
                            HttpCompletionOption.ResponseContentRead,
                            token))
                        {
                            var diagContent = diagResponse.Content;
                            var searchDiagResultRaw = await diagContent.ReadAsStringAsync();
                            var searchDiagResultObject = JsonConvert.DeserializeObject<SearchDiagnosticResponse>(searchDiagResultRaw);

                            var commitDateTime = DateTimeOffset.Parse(searchDiagResultObject.CommitUserData.CommitTimeStamp);

                            maxCommit = commitDateTime > maxCommit ? commitDateTime : maxCommit;
                        }
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
                
                var catalogLeafProcessor = new PackageLagCatalogLeafProcessor(instances, _httpClient, _telemetryService, LoggerFactory.CreateLogger<PackageLagCatalogLeafProcessor>());

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

        private async Task<List<Instance>> GetSearchEndpointsAsync(CancellationToken token)
        {
            var regionInformations = _configuration.RegionInformations;
            var subscription = _configuration.Subscription;
            var instances = new List<Instance>();

            foreach (var regionInformation in regionInformations)
            {
                string result = await _azureManagementApiWrapper.GetCloudServicePropertiesAsync(
                                        subscription,
                                        regionInformation.ResourceGroup,
                                        regionInformation.ServiceName,
                                        ProductionSlot,
                                        token);

                var cloudService = AzureHelper.ParseCloudServiceProperties(result);

                instances.AddRange(GetInstances(cloudService.Uri, cloudService.InstanceCount, regionInformation.Region));
            }

            return instances;
        }

        private List<Instance> GetInstances(Uri endpointUri, int instanceCount, string region)
        {
            var instancePortMinimum = _configuration.InstancePortMinimum;

            Logger.LogInformation("Testing {InstanceCount} instances, starting at port {InstancePortMinimum}.", instanceCount, instancePortMinimum);

            return Enumerable
                .Range(0, instanceCount)
                .Select(i =>
                {
                    var diagUriBuilder = new UriBuilder(endpointUri);

                    diagUriBuilder.Scheme = "https";
                    diagUriBuilder.Port = instancePortMinimum + i;
                    diagUriBuilder.Path = "search/diag";

                    var queryBaseUriBuilder = new UriBuilder(endpointUri);

                    queryBaseUriBuilder.Scheme = "https";
                    queryBaseUriBuilder.Port = instancePortMinimum + i;
                    queryBaseUriBuilder.Path = "search/query";

                    return new Instance
                    {
                        Index = i,
                        DiagUrl = diagUriBuilder.Uri.ToString(),
                        BaseQueryUrl = queryBaseUriBuilder.Uri.ToString(),
                        Region = region
                    };
                })
                .ToList();
        }
    }
}
