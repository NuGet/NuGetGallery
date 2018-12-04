// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using Autofac;
using Microsoft.Azure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.AzureSearch.Catalog2AzureSearch;
using NuGet.Services.AzureSearch.Db2AzureSearch;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace NuGet.Services.AzureSearch
{
    public static class DependencyInjectionExtensions
    {
        private const string SearchIndexKey = "SearchIndex";
        private const string HijackIndexKey = "HijackIndex";

        public static ContainerBuilder AddAzureSearch(this ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(c =>
                {
                    var serviceClient = c.Resolve<ISearchServiceClientWrapper>();
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return serviceClient.Indexes.GetClient(options.Value.SearchIndexName);
                })
                .SingleInstance()
                .Keyed<ISearchIndexClientWrapper>(SearchIndexKey);

            containerBuilder
                .Register(c =>
                {
                    var serviceClient = c.Resolve<ISearchServiceClientWrapper>();
                    var options = c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>();
                    return serviceClient.Indexes.GetClient(options.Value.HijackIndexName);
                })
                .SingleInstance()
                .Keyed<ISearchIndexClientWrapper>(HijackIndexKey);

            containerBuilder
                .Register<IBatchPusher>(c => new BatchPusher(
                    c.ResolveKeyed<ISearchIndexClientWrapper>(SearchIndexKey),
                    c.ResolveKeyed<ISearchIndexClientWrapper>(HijackIndexKey),
                    c.Resolve<IVersionListDataClient>(),
                    c.Resolve<IOptionsSnapshot<AzureSearchConfiguration>>(),
                    c.Resolve<ILogger<BatchPusher>>()));

            return containerBuilder;
        }

        public static IServiceCollection AddAzureSearch(this IServiceCollection services)
        {
            services.AddTransient(p => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });

            services.AddTransient(p => (HttpMessageHandler)new TelemetryHandler(
                p.GetRequiredService<ITelemetryService>(),
                p.GetRequiredService<HttpClientHandler>()));

            services.AddSingleton(p => new HttpClient(
                p.GetRequiredService<HttpMessageHandler>()));

            services.AddTransient(p =>
            {
                var options = p.GetRequiredService<IOptionsSnapshot<AzureSearchConfiguration>>();
                return CloudStorageAccount.Parse(options.Value.StorageConnectionString);
            });

            services.AddTransient(p =>
            {
                var account = p.GetRequiredService<CloudStorageAccount>();
                var options = p.GetRequiredService<IOptionsSnapshot<AzureSearchConfiguration>>();
                return (IStorageFactory)new AzureStorageFactory(
                    account,
                    CoreConstants.ContentFolderName,
                    maxExecutionTime: AzureStorage.DefaultMaxExecutionTime,
                    serverTimeout: AzureStorage.DefaultServerTimeout,
                    path: options.Value.NormalizeStoragePath(),
                    baseAddress: null,
                    useServerSideCopy: true,
                    compressContent: false,
                    verbose: true);
            });

            services.AddTransient<ISearchServiceClient>(p =>
            {
                var options = p.GetRequiredService<IOptionsSnapshot<AzureSearchConfiguration>>();
                return new SearchServiceClient(
                    options.Value.SearchServiceName,
                    new SearchCredentials(options.Value.SearchServiceApiKey));
            });

            services.AddTransient<ICloudBlobClient, CloudBlobClientWrapper>(p =>
            {
                var options = p.GetRequiredService<IOptionsSnapshot<AzureSearchConfiguration>>();
                return new CloudBlobClientWrapper(
                    options.Value.StorageConnectionString,
                    readAccessGeoRedundant: true);
            });

            services.AddTransient<ICatalogClient, CatalogClient>(p => new CatalogClient(
                p.GetRequiredService<ISimpleHttpClient>(),
                p.GetRequiredService<ILogger<CatalogClient>>()));

            services.AddTransient<ICatalogIndexActionBuilder, CatalogIndexActionBuilder>();
            services.AddTransient<ICatalogLeafFetcher, CatalogLeafFetcher>();
            services.AddTransient<ICollector, AzureSearchCollector>();
            services.AddTransient<ICommitCollectorLogic, AzureSearchCollectorLogic>();
            services.AddTransient<ICoreFileStorageService, CloudBlobCoreFileStorageService>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<IEntitiesContextFactory, EntitiesContextFactory>();
            services.AddTransient<IHijackDocumentBuilder, HijackDocumentBuilder>();
            services.AddTransient<IIndexBuilder, IndexBuilder>();
            services.AddTransient<INewPackageRegistrationProducer, NewPackageRegistrationProducer>();
            services.AddTransient<IPackageEntityIndexActionBuilder, PackageEntityIndexActionBuilder>();
            services.AddTransient<IRegistrationClient, RegistrationClient>();
            services.AddTransient<ISearchDocumentBuilder, SearchDocumentBuilder>();
            services.AddTransient<ISearchServiceClientWrapper, SearchServiceClientWrapper>();
            services.AddTransient<ISimpleHttpClient, SimpleHttpClient>();
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<IVersionListDataClient, VersionListDataClient>();

            return services;
        }
    }
}
