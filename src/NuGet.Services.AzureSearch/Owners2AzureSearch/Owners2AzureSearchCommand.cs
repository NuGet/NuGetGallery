// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class Owners2AzureSearchCommand
    {
        private readonly IDatabaseOwnerFetcher _databaseOwnerFetcher;
        private readonly IOwnerDataClient _ownerDataClient;
        private readonly IOwnerSetComparer _ownerSetComparer;
        private readonly ISearchDocumentBuilder _searchDocumentBuilder;
        private readonly ISearchIndexActionBuilder _searchIndexActionBuilder;
        private readonly Func<IBatchPusher> _batchPusherFactory;
        private readonly IOptionsSnapshot<AzureSearchJobConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<Owners2AzureSearchCommand> _logger;

        public Owners2AzureSearchCommand(
            IDatabaseOwnerFetcher databaseOwnerFetcher,
            IOwnerDataClient ownerDataClient,
            IOwnerSetComparer ownerSetComparer,
            ISearchDocumentBuilder searchDocumentBuilder,
            ISearchIndexActionBuilder indexActionBuilder,
            Func<IBatchPusher> batchPusherFactory,
            IOptionsSnapshot<AzureSearchJobConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<Owners2AzureSearchCommand> logger)
        {
            _databaseOwnerFetcher = databaseOwnerFetcher ?? throw new ArgumentNullException(nameof(databaseOwnerFetcher));
            _ownerDataClient = ownerDataClient ?? throw new ArgumentNullException(nameof(ownerDataClient));
            _ownerSetComparer = ownerSetComparer ?? throw new ArgumentNullException(nameof(ownerSetComparer));
            _searchDocumentBuilder = searchDocumentBuilder ?? throw new ArgumentNullException(nameof(searchDocumentBuilder));
            _searchIndexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _batchPusherFactory = batchPusherFactory ?? throw new ArgumentNullException(nameof(batchPusherFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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
            var stopwatch = Stopwatch.StartNew();
            var success = false;
            try
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
                    success = true;
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
                success = true;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackOwners2AzureSearchCompleted(success, stopwatch.Elapsed);
            }
        }

        private async Task WorkAsync(ConcurrentBag<IdAndValue<string[]>> changesBag)
        {
            await Task.Yield();

            var batchPusher = _batchPusherFactory();
            while (changesBag.TryTake(out var changes))
            {
                // Note that the owner list passed in can be empty (e.g. if the last owner was deleted or removed from
                // the package registration).
                var indexActions = await _searchIndexActionBuilder.UpdateAsync(
                    changes.Id,
                    searchFilters => _searchDocumentBuilder.UpdateOwners(changes.Id, searchFilters, changes.Value));

                // If no index actions are returned, this means that there are no listed packages or no
                // packages at all.
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

