// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using Autofac;
using Microsoft.Azure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation;
using NuGet.Protocol;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.AzureSearch.Auxiliary2AzureSearch;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Catalog2AzureSearch;
using NuGet.Services.AzureSearch.Db2AzureSearch;
using NuGet.Services.AzureSearch.Owners2AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace NuGet.Services.AzureSearch
{
    public static class DependencyInjectionExtensions
    {
        public static ContainerBuilder AddAzureSearch(this ContainerBuilder containerBuilder)
        {
            /// Here, we register services that depend on an interface that there are multiple implementations.
            
            /// There are multiple implementations of <see cref="ISearchServiceClientWrapper"/>.
            RegisterIndexServices(containerBuilder, "SearchIndex", "HijackIndex");

            /// There are multiple implementations of storage, in particulare <see cref="ICloudBlobClient"/>.
            RegisterAzureSearchJobStorageServices(containerBuilder, "AzureSearchJobStorage");
            RegisterAuxiliaryDataStorageServices(containerBuilder, "AuxiliaryDataStorage");

            return containerBuilder;
        }

        private static void RegisterIndexServices(ContainerBuilder containerBuilder, string searchIndexKey, string hijackIndexKey)
        {
            containerBuilder
                .Register(c =>
                {
                    var serviceClient = c.Resolve<ISearchServiceClientWrapper>();
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return serviceClient.Indexes.GetClient(options.Value.SearchIndexName);
                })
                .SingleInstance()
                .Keyed<ISearchIndexClientWrapper>(searchIndexKey);

            containerBuilder
                .Register(c =>
                {
                    var serviceClient = c.Resolve<ISearchServiceClientWrapper>();
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return serviceClient.Indexes.GetClient(options.Value.HijackIndexName);
                })
                .SingleInstance()
                .Keyed<ISearchIndexClientWrapper>(hijackIndexKey);

            containerBuilder
                .Register<IBatchPusher>(c => new BatchPusher(
                    c.ResolveKeyed<ISearchIndexClientWrapper>(searchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(hijackIndexKey),
                    c.Resolve<IVersionListDataClient>(),
                    c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<BatchPusher>>()));

            containerBuilder
                .Register<ISearchService>(c => new AzureSearchService(
                    c.Resolve<ISearchTextBuilder>(),
                    c.Resolve<ISearchParametersBuilder>(),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(searchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(hijackIndexKey),
                    c.Resolve<ISearchResponseBuilder>(),
                    c.Resolve<IAzureSearchTelemetryService>()));

            containerBuilder
                .Register<ISearchStatusService>(c => new SearchStatusService(
                    c.ResolveKeyed<ISearchIndexClientWrapper>(searchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(hijackIndexKey),
                    c.Resolve<ISearchParametersBuilder>(),
                    c.Resolve<IAuxiliaryDataCache>(),
                    c.Resolve<IOptionsSnapshot<SearchServiceConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<SearchStatusService>>()));
        }

        private static void RegisterAzureSearchJobStorageServices(ContainerBuilder containerBuilder, string key)
        {
            containerBuilder
                .Register<ICloudBlobClient>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                    return new CloudBlobClientWrapper(
                        options.Value.StorageConnectionString,
                        readAccessGeoRedundant: true);
                })
                .Keyed<ICloudBlobClient>(key);

            containerBuilder
                .Register<IVersionListDataClient>(c => new VersionListDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>(),
                    c.Resolve<ILogger<VersionListDataClient>>()));

            containerBuilder
                .Register(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                    return CloudStorageAccount.Parse(options.Value.StorageConnectionString);
                })
                .Keyed<CloudStorageAccount>(key);

            containerBuilder
                .Register<IStorageFactory>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                    return new AzureStorageFactory(
                        c.ResolveKeyed<CloudStorageAccount>(key),
                        options.Value.StorageContainer,
                        maxExecutionTime: AzureStorage.DefaultMaxExecutionTime,
                        serverTimeout: AzureStorage.DefaultServerTimeout,
                        path: options.Value.NormalizeStoragePath(),
                        baseAddress: null,
                        useServerSideCopy: true,
                        compressContent: false,
                        verbose: true,
                        initializeContainer: false,
                        throttle: NullThrottle.Instance);
                })
                .Keyed<IStorageFactory>(key);

            containerBuilder
                .Register<IBlobContainerBuilder>(c => new BlobContainerBuilder(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>(),
                    c.Resolve<ILogger<BlobContainerBuilder>>()));

            containerBuilder
                .Register<IOwnerDataClient>(c => new OwnerDataClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<AzureSearchJobConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<OwnerDataClient>>()));

            containerBuilder
                .Register(c => new Catalog2AzureSearchCommand(
                    c.Resolve<ICollector>(),
                    c.ResolveKeyed<IStorageFactory>(key),
                    c.Resolve<Func<HttpMessageHandler>>(),
                    c.Resolve<IBlobContainerBuilder>(),
                    c.Resolve<IIndexBuilder>(),
                    c.Resolve<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>(),
                    c.Resolve<ILogger<Catalog2AzureSearchCommand>>()));

            containerBuilder
                .Register(c => new Db2AzureSearchCommand(
                    c.Resolve<INewPackageRegistrationProducer>(),
                    c.Resolve<IPackageEntityIndexActionBuilder>(),
                    c.Resolve<IBlobContainerBuilder>(),
                    c.Resolve<IIndexBuilder>(),
                    c.Resolve<Func<IBatchPusher>>(),
                    c.Resolve<ICatalogClient>(),
                    c.ResolveKeyed<IStorageFactory>(key),
                    c.Resolve<IOwnerDataClient>(),
                    c.Resolve<IDownloadDataClient>(),
                    c.Resolve<IOptionsSnapshot<Db2AzureSearchConfiguration>>(),
                    c.Resolve<ILogger<Db2AzureSearchCommand>>()));
        }

        private static void RegisterAuxiliaryDataStorageServices(ContainerBuilder containerBuilder, string key)
        {
            containerBuilder
                .Register<ICloudBlobClient>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<IAuxiliaryDataStorageConfiguration>>();
                    return new CloudBlobClientWrapper(
                        options.Value.AuxiliaryDataStorageConnectionString,
                        readAccessGeoRedundant: true);
                })
                .Keyed<ICloudBlobClient>(key);

            containerBuilder
                .Register<IAuxiliaryFileClient>(c => new AuxiliaryFileClient(
                    c.ResolveKeyed<ICloudBlobClient>(key),
                    c.Resolve<IOptionsSnapshot<IAuxiliaryDataStorageConfiguration>>(),
                    c.Resolve<IAzureSearchTelemetryService>(),
                    c.Resolve<ILogger<AuxiliaryFileClient>>()));
        }

        public static IServiceCollection AddAzureSearch(this IServiceCollection services)
        {
            services
                .AddTransient(p => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });

            services
                .AddTransient(p => (HttpMessageHandler)new TelemetryHandler(
                    p.GetRequiredService<ITelemetryService>(),
                    p.GetRequiredService<HttpClientHandler>()));

            services.AddSingleton(p => new HttpClient(p.GetRequiredService<HttpMessageHandler>()));

            services
                .AddTransient<ISearchServiceClient>(p =>
                {
                    var options = p.GetRequiredService<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return new SearchServiceClient(
                        options.Value.SearchServiceName,
                        new SearchCredentials(options.Value.SearchServiceApiKey));
                });

            services
                .AddTransient<ICatalogClient, CatalogClient>(p => new CatalogClient(
                    p.GetRequiredService<ISimpleHttpClient>(),
                    p.GetRequiredService<ILogger<CatalogClient>>()));

            services.AddSingleton<IAuxiliaryDataCache, AuxiliaryDataCache>();
            services.AddScoped(p => p.GetRequiredService<IAuxiliaryDataCache>().Get());
            services.AddSingleton<IAuxiliaryFileReloader, AuxiliaryFileReloader>();

            services.AddTransient<Auxiliary2AzureSearchCommand>();
            services.AddTransient<Owners2AzureSearchCommand>();

            services.AddTransient<IAzureSearchTelemetryService, AzureSearchTelemetryService>();
            services.AddTransient<IBaseDocumentBuilder, BaseDocumentBuilder>();
            services.AddTransient<ICatalogIndexActionBuilder, CatalogIndexActionBuilder>();
            services.AddTransient<ICatalogLeafFetcher, CatalogLeafFetcher>();
            services.AddTransient<ICollector, AzureSearchCollector>();
            services.AddTransient<ICommitCollectorLogic, AzureSearchCollectorLogic>();
            services.AddTransient<IDatabaseOwnerFetcher, DatabaseOwnerFetcher>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<IEntitiesContextFactory, EntitiesContextFactory>();
            services.AddTransient<IHijackDocumentBuilder, HijackDocumentBuilder>();
            services.AddTransient<IIndexBuilder, IndexBuilder>();
            services.AddTransient<INewPackageRegistrationProducer, NewPackageRegistrationProducer>();
            services.AddTransient<IOwnerSetComparer, OwnerSetComparer>();
            services.AddTransient<IPackageEntityIndexActionBuilder, PackageEntityIndexActionBuilder>();
            services.AddTransient<IRegistrationClient, RegistrationClient>();
            services.AddTransient<ISearchDocumentBuilder, SearchDocumentBuilder>();
            services.AddTransient<ISearchIndexActionBuilder, SearchIndexActionBuilder>();
            services.AddTransient<ISearchParametersBuilder, SearchParametersBuilder>();
            services.AddTransient<ISearchResponseBuilder, SearchResponseBuilder>();
            services.AddTransient<ISearchServiceClientWrapper, SearchServiceClientWrapper>();
            services.AddTransient<ISearchTextBuilder, SearchTextBuilder>();
            services.AddTransient<ISimpleHttpClient, SimpleHttpClient>();
            services.AddTransient<ISystemTime, SystemTime>();
            services.AddTransient<ITelemetryService, TelemetryService>();

            return services;
        }
    }
}
