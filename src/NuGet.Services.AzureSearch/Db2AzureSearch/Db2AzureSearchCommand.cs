// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Catalog2AzureSearch;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchCommand
    {
        private readonly INewPackageRegistrationProducer _producer;
        private readonly IPackageEntityIndexActionBuilder _indexActionBuilder;
        private readonly IBlobContainerBuilder _blobContainerBuilder;
        private readonly IIndexBuilder _indexBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly ICatalogClient _catalogClient;
        private readonly IStorageFactory _storageFactory;
        private readonly IOwnerDataClient _ownerDataClient;
        private readonly IDownloadDataClient _downloadDataClient;
        private readonly IOptionsSnapshot<Db2AzureSearchConfiguration> _options;
        private readonly ILogger<Db2AzureSearchCommand> _logger;

        public Db2AzureSearchCommand(
            INewPackageRegistrationProducer producer,
            IPackageEntityIndexActionBuilder indexActionBuilder,
            IBlobContainerBuilder blobContainerBuilder,
            IIndexBuilder indexBuilder,
            Func<IBatchPusher> batchPusherFactory,
            ICatalogClient catalogClient,
            IStorageFactory storageFactory,
            IOwnerDataClient ownerDataClient,
            IDownloadDataClient downloadDataClient,
            IOptionsSnapshot<Db2AzureSearchConfiguration> options,
            ILogger<Db2AzureSearchCommand> logger)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _blobContainerBuilder = blobContainerBuilder ?? throw new ArgumentNullException(nameof(blobContainerBuilder));
            _indexBuilder = indexBuilder ?? throw new ArgumentNullException(nameof(indexBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _ownerDataClient = ownerDataClient ?? throw new ArgumentNullException(nameof(ownerDataClient));
            _downloadDataClient = downloadDataClient ?? throw new ArgumentNullException(nameof(downloadDataClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentBatches)} must be greater than zero.");
            }
        }

        public async Task ExecuteAsync()
        {
            await ExecuteAsync(CancellationToken.None);
        }

        private async Task ExecuteAsync(CancellationToken token)
        {
            using (var cancelledCts = new CancellationTokenSource())
            using (var produceWorkCts = new CancellationTokenSource())
            {
                // Initialize the indexes, container and excluded packages data.
                await InitializeAsync();

                // Here, we fetch the current catalog timestamp to use as the initial cursor value for
                // catalog2azuresearch. The idea here is that database is always more up-to-date than the catalog.
                // We're about to read the database so if we capture a catalog timestamp now, we are guaranteed that
                // any data we get from a database query will be more recent than the data represented by this catalog
                // timestamp. When catalog2azuresearch starts up for the first time to update the index produced by this
                // job, it will probably encounter some duplicate packages, but this is okay.
                //
                // Note that we could capture any dependency cursors here instead of catalog cursor, but this is
                // pointless because there is no reliable way to filter out data fetched from the database based on a
                // catalog-based cursor value. Suppose the dependency cursor is catalog2registration. If
                // catalog2registration is very behind, then the index produced by this job will include packages that
                // are not yet restorable (since they are not in the registration hives). This could lead to a case
                // where a user is able to search for a package that he cannot restore. We mitigate this risk by
                // trusting that our end-to-end tests will fail when catalog2registration (or any other V3 component) is
                // broken, this blocking the deployment of new Azure Search indexes.
                var catalogIndex = await _catalogClient.GetIndexAsync(_options.Value.CatalogIndexUrl);
                var initialCursorValue = catalogIndex.CommitTimestamp;
                _logger.LogInformation("The initial cursor value will be {CursorValue:O}.", initialCursorValue);

                // Push all package package data to the Azure Search indexes and write the version list blobs.
                var allOwners = new ConcurrentBag<IdAndValue<IReadOnlyList<string>>>();
                var allDownloads = new ConcurrentBag<DownloadRecord>();

                await PushAllPackageRegistrationsAsync(cancelledCts, produceWorkCts, allOwners, allDownloads);

                // Write the owner data file.
                await WriteOwnerDataAsync(allOwners);

                // Write the download data file.
                await WriteDownloadDataAsync(allDownloads);

                // Write the cursor.
                _logger.LogInformation("Writing the initial cursor value to be {CursorValue:O}.", initialCursorValue);
                var frontCursorStorage = _storageFactory.Create();
                var frontCursor = new DurableCursor(
                    frontCursorStorage.ResolveUri(Catalog2AzureSearchCommand.CursorRelativeUri),
                    frontCursorStorage,
                    DateTime.MinValue);
                frontCursor.Value = initialCursorValue.UtcDateTime;
                await frontCursor.SaveAsync(token);
            }
        }

        private async Task InitializeAsync()
        {
            var containerDeleted = false;
            if (_options.Value.ReplaceContainersAndIndexes)
            {
                containerDeleted = await _blobContainerBuilder.DeleteIfExistsAsync();
                await _indexBuilder.DeleteSearchIndexIfExistsAsync();
                await _indexBuilder.DeleteHijackIndexIfExistsAsync();
            }

            await _blobContainerBuilder.CreateAsync(containerDeleted);
            await _indexBuilder.CreateSearchIndexAsync();
            await _indexBuilder.CreateHijackIndexAsync();
        }

        private async Task PushAllPackageRegistrationsAsync(
            CancellationTokenSource cancelledCts,
            CancellationTokenSource produceWorkCts,
            ConcurrentBag<IdAndValue<IReadOnlyList<string>>> allOwners,
            ConcurrentBag<DownloadRecord> allDownloads)
        {
            _logger.LogInformation("Pushing all packages to Azure Search and initializing version lists.");
            var allWork = new ConcurrentBag<NewPackageRegistration>();
            var producerTask = ProduceWorkAsync(allWork, produceWorkCts, cancelledCts.Token);
            var consumerTasks = Enumerable
                .Range(0, _options.Value.MaxConcurrentBatches)
                .Select(i => ConsumeWorkAsync(allWork, allOwners, allDownloads, produceWorkCts.Token, cancelledCts.Token))
                .ToList();
            var allTasks = new[] { producerTask }.Concat(consumerTasks).ToList();

            // If one of the tasks throws an exception before the work is completed, cancel the work.
            var firstTask = await Task.WhenAny(allTasks);
            if (firstTask.IsFaulted)
            {
                cancelledCts.Cancel();
            }

            await firstTask;
            await Task.WhenAll(allTasks);
            _logger.LogInformation("Done initializing the Azure Search indexes and version lists.");
        }

        private async Task WriteOwnerDataAsync(ConcurrentBag<IdAndValue<IReadOnlyList<string>>> allOwners)
        {
            _logger.LogInformation("Building and writing the initial owners file.");
            var ownersBuilder = new PackageIdToOwnersBuilder(_logger);
            foreach (var owners in allOwners)
            {
                ownersBuilder.Add(owners.Id, owners.Value);
            }

            await _ownerDataClient.ReplaceLatestIndexedAsync(
                ownersBuilder.GetResult(),
                AccessConditionWrapper.GenerateIfNotExistsCondition());
            _logger.LogInformation("Done uploading the initial owners file.");
        }

        private async Task WriteDownloadDataAsync(ConcurrentBag<DownloadRecord> allDownloads)
        {
            _logger.LogInformation("Building and writing the initial download data file.");
            var downloadData = new DownloadData();
            foreach (var dr in allDownloads)
            {
                downloadData.SetDownloadCount(dr.PackageId, dr.NormalizedVersion, dr.DownloadCount);
            }

            await _downloadDataClient.ReplaceLatestIndexedAsync(
                downloadData,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
            _logger.LogInformation("Done uploading the initial download data file.");
        }

        private async Task ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationTokenSource produceWorkCts,
            CancellationToken cancellationToken)
        {

            await Task.Yield();
            await _producer.ProduceWorkAsync(allWork, cancellationToken);
            produceWorkCts.Cancel();
        }

        private async Task ConsumeWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            ConcurrentBag<IdAndValue<IReadOnlyList<string>>> allOwners,
            ConcurrentBag<DownloadRecord> allDownloads,
            CancellationToken produceWorkToken,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            var batchPusher = _batchPusherFactory();

            NewPackageRegistration work = null;
            try
            {
                while ((allWork.TryTake(out work) || !produceWorkToken.IsCancellationRequested)
                    && !cancellationToken.IsCancellationRequested)
                {
                    // If there's no work to do, wait a bit before checking again.
                    if (work == null)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                        continue;
                    }

                    var indexActions = _indexActionBuilder.AddNewPackageRegistration(work);

                    // There can be an empty set of index actions if there were no packages associated with this
                    // package registration.
                    if (!indexActions.IsEmpty)
                    {
                        batchPusher.EnqueueIndexActions(work.PackageId, indexActions);
                        await batchPusher.PushFullBatchesAsync();
                    }

                    // Keep track of all owners so we can write them to the initial owners.v2.json file.
                    allOwners.Add(new IdAndValue<IReadOnlyList<string>>(work.PackageId, work.Owners));

                    // Keep track of all download counts so we can write them to the initial downloads.v2.json file.
                    foreach (var package in work.Packages)
                    {
                        allDownloads.Add(new DownloadRecord(work.PackageId, package.NormalizedVersion, package.DownloadCount));
                    }
                }

                await batchPusher.FinishAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    0,
                    ex,
                    "An exception was thrown while processing package ID {PackageId}.",
                    work?.PackageId ?? "(last batch...)");
                throw;
            }
        }

        private struct DownloadRecord
        {
            public DownloadRecord(string packageId, string normalizedVersion, int downloadCount)
            {
                PackageId = packageId;
                NormalizedVersion = normalizedVersion;
                DownloadCount = downloadCount;
            }

            public string PackageId { get; }
            public string NormalizedVersion { get; }
            public int DownloadCount { get; }
        }
    }
}
