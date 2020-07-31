// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class UpdateVerifiedPackagesCommand : IAzureSearchCommand
    {
        private readonly IDatabaseAuxiliaryDataFetcher _databaseFetcher;
        private readonly IVerifiedPackagesDataClient _verifiedPackagesDataClient;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<Auxiliary2AzureSearchCommand> _logger;
        private readonly StringCache _stringCache;

        public UpdateVerifiedPackagesCommand(
            IDatabaseAuxiliaryDataFetcher databaseFetcher,
            IVerifiedPackagesDataClient verifiedPackagesDataClient,
            IAzureSearchTelemetryService telemetryService,
            ILogger<Auxiliary2AzureSearchCommand> logger)
        {
            _databaseFetcher = databaseFetcher ?? throw new ArgumentNullException(nameof(databaseFetcher));
            _verifiedPackagesDataClient = verifiedPackagesDataClient ?? throw new ArgumentNullException(nameof(verifiedPackagesDataClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCache = new StringCache();
        }

        public async Task ExecuteAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var outcome = JobOutcome.Failure;
            try
            {
                outcome = await UpdateVerifiedPackagesAsync() ? JobOutcome.Success : JobOutcome.NoOp;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryService.TrackUpdateVerifiedPackagesCompleted(outcome, stopwatch.Elapsed);
            }
        }

        private async Task<bool> UpdateVerifiedPackagesAsync()
        {
            // The "old" data in this case is the latest file that was copied to the region's storage container by this
            // job (or initialized by Db2AzureSearch).
            var oldResult = await _verifiedPackagesDataClient.ReadLatestAsync(
                AccessConditionWrapper.GenerateEmptyCondition(),
                _stringCache);

            // The "new" data in this case is from the database.
            var newData = await _databaseFetcher.GetVerifiedPackagesAsync();

            var changes = new HashSet<string>(oldResult.Data, oldResult.Data.Comparer);
            changes.SymmetricExceptWith(newData);
            _logger.LogInformation("{Count} package IDs have verified status changes.", changes.Count);

            if (changes.Count == 0)
            {
                return false;
            }
            else
            {
                await _verifiedPackagesDataClient.ReplaceLatestAsync(newData, oldResult.Metadata.GetIfMatchCondition());
                return true;
            }
        }
    }
}
