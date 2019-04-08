// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ng;
using Ng.Jobs;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

namespace NuGet.Services.V3PerPackage
{
    public class PerBatchProcessor
    {
        private readonly HttpMessageHandler _httpMessageHandler;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TelemetryClient _telemetryClient;
        private readonly StringLocker _stringLocker;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PerBatchProcessor> _logger;

        public PerBatchProcessor(
            HttpMessageHandler httpMessageHandler,
            ILoggerFactory loggerFactory,
            TelemetryClient telemetryClient,
            StringLocker stringLocker,
            HttpClient httpClient,
            ILogger<PerBatchProcessor> logger)
        {
            _httpMessageHandler = httpMessageHandler;
            _loggerFactory = loggerFactory;
            _telemetryClient = telemetryClient;
            _stringLocker = stringLocker;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> ProcessAsync(PerBatchContext context, IReadOnlyList<PerPackageContext> packageContexts)
        {
            packageContexts = await GetExistingPackagesAsync(packageContexts);

            if (!packageContexts.Any())
            {
                return true;
            }

            var catalogIndexUri = await ExecuteFeedToCatalogAsync(context, packageContexts);

            await ExecuteCatalog2DnxAsync(context, packageContexts, catalogIndexUri);

            await ExecuteCatalog2RegistrationAsync(context, catalogIndexUri);

            await ExecuteCatalog2LuceneAsync(context, catalogIndexUri);

            return true;
        }

        private async Task<IReadOnlyList<PerPackageContext>> GetExistingPackagesAsync(IReadOnlyList<PerPackageContext> packageContexts)
        {
            var tasks = packageContexts
                .Select(x => new { Context = x, Task = DoesPackageExistAsync(x) })
                .ToList();

            await Task.WhenAll(tasks.Select(x => x.Task));

            return tasks
                .Where(x => x.Task.Result)
                .Select(x => x.Context)
                .ToList();
        }

        private async Task<bool> DoesPackageExistAsync(PerPackageContext packageContext)
        {
            // Note that if the package for some reason exists in V2 (database) but is missing from the packages
            // container, this will skip the package. We need the .nupkg to generate V3 artifacts so there's not much
            // else we can do.
            using (var request = new HttpRequestMessage(HttpMethod.Head, packageContext.PackageUri))
            using (var response = await _httpClient.SendAsync(request))
            {
                var exists = response.StatusCode != HttpStatusCode.NotFound;

                if (!exists)
                {
                    _logger.LogInformation(
                        "Package {Id}/{Version} no longer exists.",
                        packageContext.PackageId,
                        packageContext.PackageVersion);
                }

                return exists;
            }
        }

        public async Task CleanUpAsync(PerBatchContext context, IReadOnlyList<PerPackageContext> packageContexts)
        {
            var blobClient = BlobStorageUtilities.GetBlobClient(context.Global);

            // Delete catalog2lucene artifacts.
            await CleanUpUtilities.DeleteContainer(blobClient, context.LuceneContainerName, _logger);

            var luceneCachePath = Path.Combine(
                CleanUpUtilities.GetLuceneCacheDirectory(),
                context.LuceneContainerName);
            if (Directory.Exists(luceneCachePath))
            {
                Directory.Delete(luceneCachePath, recursive: true);
            }

            // Delete catalog2registration artifacts.
            await CleanUpUtilities.DeleteBlobsWithPrefix(
                blobClient,
                context.Global.RegistrationContainerName,
                context.Worker.Name,
                _logger);

            // Delete catalog2dnx artifacts.
            foreach (var packageContext in packageContexts)
            {
                await CleanUpUtilities.DeleteBlobsWithPrefix(
                    blobClient,
                    context.Global.FlatContainerContainerName,
                    $"{context.Process.FlatContainerStoragePath}/{packageContext.PackageId.ToLowerInvariant()}/{packageContext.PackageVersion.ToLowerInvariant()}",
                    _logger);
            }

            // Delete feed2catalog artifacts.
            await CleanUpUtilities.DeleteBlobsWithPrefix(
                blobClient,
                context.Global.CatalogContainerName,
                context.Worker.Name,
                _logger);
        }

        private async Task<Uri> ExecuteFeedToCatalogAsync(PerBatchContext context, IReadOnlyList<PerPackageContext> packageContexts)
        {
            var serviceProvider = GetServiceProvider(
                context,
                context.Global.CatalogContainerName,
                context.Worker.CatalogStoragePath);

            var now = DateTime.UtcNow;
            var offset = 0;
            var packages = new SortedList<DateTime, IList<FeedPackageDetails>>();
            var maxDegreeOfParallelism = ServicePointManager.DefaultConnectionLimit;

            foreach (var packageContext in packageContexts)
            {
                // These timestamps don't matter too much since the order that items are processed within a catalog
                // commit is not defined. This is just a convenient way to get a bunch of unique timestamps to ease
                // debugging.
                var key = now.AddSeconds(offset--);
                var published = now.AddSeconds(offset--);
                var lastEdited = now.AddSeconds(offset--);
                var created = now.AddSeconds(offset--);

                packages.Add(key, new List<FeedPackageDetails>
                {
                    new FeedPackageDetails(
                        packageContext.PackageUri,
                        created,
                        lastEdited,
                        published,
                        packageContext.PackageId,
                        packageContext.PackageVersion)
                });
            }

            var storage = serviceProvider.GetRequiredService<IStorage>();
            var createdPackages = true;
            var updateCreatedFromEdited = false;

            using (var httpClient = serviceProvider.GetRequiredService<HttpClient>())
            {
                var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
                var logger = serviceProvider.GetRequiredService<ILogger>();

                var packageCatalogItemCreator = PackageCatalogItemCreator.Create(
                    httpClient,
                    telemetryService,
                    logger,
                    storage: null);

                await FeedHelpers.DownloadMetadata2CatalogAsync(
                    packageCatalogItemCreator,
                    packages,
                    storage,
                    now,
                    now,
                    now,
                    maxDegreeOfParallelism,
                    createdPackages,
                    updateCreatedFromEdited,
                    CancellationToken.None,
                    telemetryService,
                    logger);
            }

            return storage.ResolveUri("index.json");
        }

        private async Task ExecuteCatalog2DnxAsync(PerBatchContext context, IReadOnlyList<PerPackageContext> packageContexts, Uri catalogIndexUri)
        {
            var serviceProvider = GetServiceProvider(
                context,
                context.Global.FlatContainerContainerName,
                context.Process.FlatContainerStoragePath);

            var storageFactory = serviceProvider.GetRequiredService<StorageFactory>();
            IAzureStorage preferredPackageSourceStorage = null;
            var httpClientTimeout = TimeSpan.FromMinutes(10);
            var maxDegreeOfParallelism = ServicePointManager.DefaultConnectionLimit;

            var collector = new DnxCatalogCollector(
                catalogIndexUri,
                storageFactory,
                preferredPackageSourceStorage,
                context.Global.ContentBaseAddress,
                serviceProvider.GetRequiredService<ITelemetryService>(),
                serviceProvider.GetRequiredService<ILogger>(),
                maxDegreeOfParallelism,
                () => serviceProvider.GetRequiredService<HttpMessageHandler>(),
                httpClientTimeout);

            var lowercasePackageIds = packageContexts.Select(x => x.PackageId.ToLowerInvariant());
            using (await _stringLocker.AcquireAsync(lowercasePackageIds, TimeSpan.FromMinutes(5)))
            {
                await collector.RunAsync(CancellationToken.None);
            }
        }

        private async Task ExecuteCatalog2RegistrationAsync(PerBatchContext context, Uri catalogIndexUri)
        {
            var serviceProvider = GetServiceProvider(
                context,
                context.Global.RegistrationContainerName,
                storagePath: null);

            var registrationStorageFactories = serviceProvider.GetRequiredService<RegistrationStorageFactories>();

            var collector = new RegistrationCollector(
                catalogIndexUri,
                registrationStorageFactories.LegacyStorageFactory,
                registrationStorageFactories.SemVer2StorageFactory,
                context.Global.ContentBaseAddress,
                context.Global.GalleryBaseAddress,
                serviceProvider.GetRequiredService<ITelemetryService>(),
                _logger,
                () => serviceProvider.GetRequiredService<HttpMessageHandler>());

            await collector.RunAsync(CancellationToken.None);
        }

        private async Task ExecuteCatalog2LuceneAsync(PerBatchContext context, Uri catalogIndexUri)
        {
            var serviceProvider = GetServiceProvider(
                context,
                context.LuceneContainerName,
                storagePath: null);

            using (var directory = serviceProvider.GetRequiredService<Lucene.Net.Store.Directory>())
            using (var indexWriter = Catalog2LuceneJob.CreateIndexWriter(directory))
            {
                var commitEachBatch = false;
                TimeSpan? commitTimeout = null;
                string baseAddress = null;

                var collector = new SearchIndexFromCatalogCollector(
                    catalogIndexUri,
                    indexWriter,
                    commitEachBatch,
                    commitTimeout,
                    baseAddress,
                    context.Global.GalleryBaseAddress,
                    serviceProvider.GetRequiredService<ITelemetryService>(),
                    serviceProvider.GetRequiredService<ILogger>(),
                    () => serviceProvider.GetRequiredService<HttpMessageHandler>());

                await collector.RunAsync(CancellationToken.None);
            }
        }

        private IServiceProvider GetServiceProvider(
            PerBatchContext context,
            string containerName,
            string storagePath)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton(_loggerFactory);
            serviceCollection.AddSingleton(_telemetryClient);
            serviceCollection.AddSingleton(_httpMessageHandler);
            serviceCollection.AddSingleton(x => new HttpClient(x.GetRequiredService<HttpMessageHandler>()));

            serviceCollection.AddTransient<ITelemetryService, TelemetryService>();
            serviceCollection.AddTransient(x => x.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program)));
            serviceCollection.AddTransient<IStorageFactory, StorageFactory>(x => x.GetRequiredService<StorageFactory>());
            serviceCollection.AddTransient<IStorage>(x => x.GetRequiredService<IStorageFactory>().Create());

            serviceCollection.AddTransient(x => CommandHelpers.CreateStorageFactory(new Dictionary<string, string>
            {
                { Arguments.StorageType, Arguments.AzureStorageType },

                { Arguments.StorageBaseAddress, $"{context.Global.StorageBaseAddress}/{containerName}/{storagePath}/" },
                { Arguments.StorageAccountName, context.Global.StorageAccountName },
                { Arguments.StorageContainer, containerName },
                { Arguments.StorageKeyValue, context.Global.StorageKeyValue },
                { Arguments.StoragePath, storagePath },
            }, verbose: true));

            serviceCollection.AddTransient(x =>
            {
                return CommandHelpers.CreateRegistrationStorageFactories(new Dictionary<string, string>
                {
                    { Arguments.StorageType, Arguments.AzureStorageType },

                    { Arguments.StorageBaseAddress, $"{context.Global.StorageBaseAddress}/{containerName}/{context.Worker.RegistrationLegacyStoragePath}/" },
                    { Arguments.StorageAccountName, context.Global.StorageAccountName },
                    { Arguments.StorageContainer, containerName },
                    { Arguments.StorageKeyValue, context.Global.StorageKeyValue },
                    { Arguments.StoragePath, context.Worker.RegistrationLegacyStoragePath },

                    { Arguments.UseCompressedStorage, "true" },
                    { Arguments.CompressedStorageBaseAddress, $"{context.Global.StorageBaseAddress}/{containerName}/{context.Worker.RegistrationCompressedStoragePath}/" },
                    { Arguments.CompressedStorageAccountName, context.Global.StorageAccountName },
                    { Arguments.CompressedStorageContainer, containerName },
                    { Arguments.CompressedStorageKeyValue, context.Global.StorageKeyValue },
                    { Arguments.CompressedStoragePath, context.Worker.RegistrationCompressedStoragePath },

                    { Arguments.UseSemVer2Storage, "true" },
                    { Arguments.SemVer2StorageBaseAddress, $"{context.Global.StorageBaseAddress}/{containerName}/{context.Worker.RegistrationSemVer2StoragePath}/" },
                    { Arguments.SemVer2StorageAccountName, context.Global.StorageAccountName },
                    { Arguments.SemVer2StorageContainer, containerName },
                    { Arguments.SemVer2StorageKeyValue, context.Global.StorageKeyValue },
                    { Arguments.SemVer2StoragePath, context.Worker.RegistrationSemVer2StoragePath },
                }, verbose: true);
            });

            serviceCollection.AddTransient(x => CommandHelpers.GetLuceneDirectory(new Dictionary<string, string>
            {
                { Arguments.LuceneDirectoryType, Arguments.AzureStorageType },
                { Arguments.LuceneStorageAccountName, context.Global.StorageAccountName },
                { Arguments.LuceneStorageContainer, containerName },
                { Arguments.LuceneStorageKeyValue, context.Global.StorageKeyValue },
            }));

            return serviceCollection.BuildServiceProvider();
        }
    }
}