// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class FixCatalogCachingJob : NgJob
    {
        private string _source;
        private string _itemCacheControl;
        private string _finishedPageCacheControl;
        private StorageFactory _catalogStorageFactory;
        private AzureStorage _catalogStorage;
        private DurableCursor _front;
        private MemoryCursor _back;
        private Func<HttpMessageHandler> _messageHandlerFactory;
        private FixCatalogCachingCollector _collector;

        public FixCatalogCachingJob(
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
            : base(loggerFactory, telemetryClient, telemetryGlobalDimensions)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng fixcatalogcaching "
                   + $"-{Arguments.Source} <catalog> "
                   + $"-{Arguments.ItemCacheControl} <item-cache-control> "
                   + $"-{Arguments.FinishedPageCacheControl} <finished-page-cache-control> "
                   + $"-{Arguments.StorageType} azure "
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageAccountName} <azure-acc> "
                   + $"-{Arguments.StorageSasValue} <azure-sas> "
                   + $"-{Arguments.StorageContainer} <azure-container> "
                   + $"-{Arguments.StoragePath} <path> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.UseManagedIdentity} true|false "
                   + $"-{Arguments.ClientId} <keyvault-client-id> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"[-{Arguments.ValidateCertificate} true|false ]]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _source = arguments.GetOrThrow<string>(Arguments.Source);
            _itemCacheControl = arguments.GetOrThrow<string>(Arguments.ItemCacheControl);
            _finishedPageCacheControl = arguments.GetOrThrow<string>(Arguments.FinishedPageCacheControl);

            _catalogStorageFactory = CommandHelpers.CreateStorageFactory(
                arguments,
                verbose: true,
                new SemaphoreSlimThrottle(new SemaphoreSlim(ServicePointManager.DefaultConnectionLimit)));
            _catalogStorage = (AzureStorage)_catalogStorageFactory.Create();

            _front = new DurableCursor(_catalogStorage.ResolveUri("fix-caching-cursor.json"), _catalogStorage, MemoryCursor.MinValue);
            _back = MemoryCursor.CreateMax();

            _messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose: true);
            _collector = new FixCatalogCachingCollector(
                _catalogStorage,
                _itemCacheControl,
                new Uri(_source),
                TelemetryService,
                _messageHandlerFactory,
                LoggerFactory.CreateLogger<FixCatalogCachingCollector>());
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            // Update the catalog page documents from newest to oldest.
            Logger.LogInformation("Starting on page cache control.");
            await UpdatePageCacheControl(cancellationToken);

            // Update the catalog item documents using catalog collector logic (oldest to newest with a cursor).
            Logger.LogInformation("Starting on leaf cache control.");
            bool run;
            do
            {
                run = await _collector.RunAsync(_front, _back, cancellationToken);
            }
            while (run);
        }

        public async Task UpdatePageCacheControl(CancellationToken cancellationToken)
        {
            using (HttpMessageHandler handler = _messageHandlerFactory())
            using (CollectorHttpClient httpClient = new CollectorHttpClient(handler))
            {
                Logger.LogInformation("Loading catalog index from {Source}.", _source);
                var index = await httpClient.GetJObjectAsync(new Uri(_source));

                var newestToOldestPages = index["items"]
                    .Select(x => CatalogCommit.Create((JObject)x))
                    .OrderByDescending(x => x.CommitTimeStamp)
                    .Skip(1) // Skip the very newest page, which may not be finished yet. Db2Catalog will update it.
                    .ToList();
                var oldestPage = newestToOldestPages.Last();

                Logger.LogInformation("Checking oldest page {PageUri}.", oldestPage.Uri.AbsoluteUri);
                var isOldestSet = await _catalogStorage.HasPropertiesAsync(
                    oldestPage.Uri,
                    "application/json",
                    _finishedPageCacheControl);

                if (isOldestSet)
                {
                    Logger.LogInformation("The oldest page has cache control properly set. No more work is needed.");
                    return;
                }

                foreach (var page in newestToOldestPages)
                {
                    var updated = await _catalogStorage.UpdateCacheControlAsync(
                        page.Uri,
                        _finishedPageCacheControl,
                        cancellationToken);
                    Logger.LogInformation("Page {PageUri}: Cache-Control updated = {Updated}", page.Uri.AbsoluteUri, updated);
                    TelemetryService.TrackCacheControlUpdate(page.Uri, _finishedPageCacheControl, updated);
                }
            }
        }

        public class FixCatalogCachingCollector : CommitCollector
        {
            private readonly Storage _catalogStorage;
            private readonly string _itemCacheControl;
            private readonly ILogger<FixCatalogCachingCollector> _logger;

            public FixCatalogCachingCollector(
                Storage catalogStorage,
                string itemCacheControl,
                Uri index,
                ITelemetryService telemetryService,
                Func<HttpMessageHandler> handlerFunc,
                ILogger<FixCatalogCachingCollector> logger)
                : base(index, telemetryService, handlerFunc)
            {
                _catalogStorage = catalogStorage;
                _itemCacheControl = itemCacheControl;
                _logger = logger;
            }

            protected override async Task<bool> OnProcessBatchAsync(
                CollectorHttpClient client,
                IEnumerable<CatalogCommitItem> items,
                JToken context,
                DateTime commitTimeStamp,
                bool isLastBatch,
                CancellationToken cancellationToken)
            {
                var count = 0;
                var updatedCount = 0;
                foreach (var item in items)
                {
                    var updated = await _catalogStorage.UpdateCacheControlAsync(
                        item.Uri,
                        _itemCacheControl,
                        cancellationToken);
                    count++;
                    updatedCount += updated ? 1 : 0;
                    _telemetryService.TrackCacheControlUpdate(item.Uri, _itemCacheControl, updated);
                }

                _logger.LogInformation("Batch of {Count} items completed. {UpdatedCount} were updated.", count, updatedCount);

                return true;
            }
        }
    }
}
