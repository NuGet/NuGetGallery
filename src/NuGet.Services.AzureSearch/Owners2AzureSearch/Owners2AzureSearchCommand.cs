// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class Owners2AzureSearchCommand
    {
        private readonly IDatabaseOwnerFetcher _databaseOwnerFetcher;
        private readonly IOwnerDataClient _ownerDataClient;
        private readonly IOwnerSetComparer _ownerSetComparer;
        private readonly IOwnerIndexActionBuilder _indexActionBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly IOptionsSnapshot<AzureSearchJobConfiguration> _options;
        private readonly ILogger<Owners2AzureSearchCommand> _logger;

        public Owners2AzureSearchCommand(
            IDatabaseOwnerFetcher databaseOwnerFetcher,
            IOwnerDataClient ownerDataClient,
            IOwnerSetComparer ownerSetComparer,
            IOwnerIndexActionBuilder indexActionBuilder,
            Func<IBatchPusher> batchPusherFactory,
            IOptionsSnapshot<AzureSearchJobConfiguration> options,
            ILogger<Owners2AzureSearchCommand> logger)
        {
            _databaseOwnerFetcher = databaseOwnerFetcher ?? throw new ArgumentNullException(nameof(databaseOwnerFetcher));
            _ownerDataClient = ownerDataClient ?? throw new ArgumentNullException(nameof(ownerDataClient));
            _ownerSetComparer = ownerSetComparer ?? throw new ArgumentNullException(nameof(ownerSetComparer));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentException(
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentBatches)} must be greater than zero.",
                    nameof(options));
            }
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Fetching old owner data from blob storage.");
            var storageResult = await _ownerDataClient.ReadLatestIndexedAsync();

            _logger.LogInformation("Fetching new owner data from the database.");
            var databaseResult = await _databaseOwnerFetcher.GetPackageIdToOwnersAsync();

            _logger.LogInformation("Detecting owner changes.");
            var changes = _ownerSetComparer.Compare(storageResult.Result, databaseResult);
            var changesBag = new ConcurrentBag<IdAndValue<string[]>>(changes.Select(x => new IdAndValue<string[]>(x.Key, x.Value)));
            _logger.LogInformation("{Count} package IDs have owner changes.", changesBag.Count);

            if (!changes.Any())
            {
                return;
            }

            _logger.LogInformation(
                "Starting {Count} workers pushing owners changes to Azure Search.",
                _options.Value.MaxConcurrentBatches);
            await ParallelAsync.Repeat(() => WorkAsync(changesBag), _options.Value.MaxConcurrentBatches);
            _logger.LogInformation("All of the owner changes have been pushed to Azure Search.");

            // Persist in storage the list of all package IDs that have owner changes. This allows debugging and future
            // analytics on frequency of ownership changes.
            _logger.LogInformation("Uploading the package IDs that have owner changes to blob storage.");
            await _ownerDataClient.UploadChangeHistoryAsync(changes.Keys.ToList());

            _logger.LogInformation("Uploading the new owner data to blob storage.");
            await _ownerDataClient.ReplaceLatestIndexedAsync(databaseResult, storageResult.AccessCondition);
        }

        private async Task WorkAsync(ConcurrentBag<IdAndValue<string[]>> changesBag)
        {
            await Task.Yield();

            var batchPusher = _batchPusherFactory();
            while (changesBag.TryTake(out var changes))
            {
                var indexActions = await _indexActionBuilder.UpdateOwnersAsync(changes.Id, changes.Value);
                if (indexActions.IsEmpty)
                {
                    continue;
                }

                batchPusher.EnqueueIndexActions(changes.Id, indexActions);
                await batchPusher.PushFullBatchesAsync();
            }

            await batchPusher.FinishAsync();
        }
    }
}

