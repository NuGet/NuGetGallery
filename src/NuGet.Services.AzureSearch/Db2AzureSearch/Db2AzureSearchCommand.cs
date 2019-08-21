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
        private readonly IVerifiedPackagesDataClient _verifiedPackagesDataClient;
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
            IVerifiedPackagesDataClient verifiedPackagesDataClient,
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
            _verifiedPackagesDataClient = verifiedPackagesDataClient ?? throw new ArgumentNullException(nameof(verifiedPackagesDataClient));
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

                var initialAuxiliaryData = await PushAllPackageRegistrationsAsync(cancelledCts, produceWorkCts);

                // Write the owner data file.
                await WriteOwnerDataAsync(initialAuxiliaryData.Owners);

                // Write the download data file.
                await WriteDownloadDataAsync(initialAuxiliaryData.Downloads);

                // Write the verified packages data file.
                await WriteVerifiedPackagesDataAsync(initialAuxiliaryData.VerifiedPackages);

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

        private async Task<InitialAuxiliaryData> PushAllPackageRegistrationsAsync(
            CancellationTokenSource cancelledCts,
            CancellationTokenSource produceWorkCts)
        {
            _logger.LogInformation("Pushing all packages to Azure Search and initializing version lists.");
            var allWork = new ConcurrentBag<NewPackageRegistration>();
            var producerTask = ProduceWorkAsync(allWork, produceWorkCts, cancelledCts.Token);
            var consumerTasks = Enumerable
                .Range(0, _options.Value.MaxConcurrentBatches)
                .Select(i => ConsumeWorkAsync(allWork, produceWorkCts.Token, cancelledCts.Token))
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

            return await producerTask;
        }

        private async Task WriteOwnerDataAsync(SortedDictionary<string, SortedSet<string>> owners)
        {
            _logger.LogInformation("Writing the initial owners file.");
            await _ownerDataClient.ReplaceLatestIndexedAsync(
                owners,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
            _logger.LogInformation("Done uploading the initial owners file.");
        }

        private async Task WriteDownloadDataAsync(DownloadData downloadData)
        {
            _logger.LogInformation("Writing the initial download data file.");
            await _downloadDataClient.ReplaceLatestIndexedAsync(
                downloadData,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
            _logger.LogInformation("Done uploading the initial download data file.");
        }

        private async Task WriteVerifiedPackagesDataAsync(HashSet<string> verifiedPackages)
        {
            _logger.LogInformation("Writing the initial verified packages data file.");
            await _verifiedPackagesDataClient.ReplaceLatestAsync(
                verifiedPackages,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
            _logger.LogInformation("Done uploading the initial verified packages data file.");
        }

        private async Task<InitialAuxiliaryData> ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationTokenSource produceWorkCts,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var output = await _producer.ProduceWorkAsync(allWork, cancellationToken);
            produceWorkCts.Cancel();
            return output;
        }

        private async Task ConsumeWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
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
    }
}
