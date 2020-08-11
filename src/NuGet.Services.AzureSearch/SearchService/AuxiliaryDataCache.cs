// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryDataCache : IAuxiliaryDataCache
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly IDownloadDataClient _downloadDataClient;
        private readonly IVerifiedPackagesDataClient _verifiedPackagesDataClient;
        private readonly IPopularityTransferDataClient _popularityTransferDataClient;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<AuxiliaryDataCache> _logger;
        private readonly StringCache _stringCache;
        private AuxiliaryData _data;

        public AuxiliaryDataCache(
            IDownloadDataClient downloadDataClient,
            IVerifiedPackagesDataClient verifiedPackagesDataClient,
            IPopularityTransferDataClient popularityTransferDataClient,
            IAzureSearchTelemetryService telemetryService,
            ILogger<AuxiliaryDataCache> logger)
        {
            _downloadDataClient = downloadDataClient ?? throw new ArgumentNullException(nameof(downloadDataClient));
            _verifiedPackagesDataClient = verifiedPackagesDataClient ?? throw new ArgumentNullException(nameof(verifiedPackagesDataClient));
            _popularityTransferDataClient = popularityTransferDataClient ?? throw new ArgumentNullException(nameof(popularityTransferDataClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCache = new StringCache();
        }

        public bool Initialized => _data != null;

        public async Task EnsureInitializedAsync()
        {
            if (!Initialized)
            {
                await LoadAsync(Timeout.InfiniteTimeSpan, shouldReload: false, token: CancellationToken.None);
            }
        }

        public async Task TryLoadAsync(CancellationToken token)
        {
            await LoadAsync(TimeSpan.Zero, shouldReload: true, token: token);
        }

        private async Task LoadAsync(TimeSpan timeout, bool shouldReload, CancellationToken token)
        {
            var acquired = false;
            try
            {
                acquired = await _lock.WaitAsync(timeout, token);
                if (!acquired)
                {
                    _logger.LogInformation("Another thread is already reloading the auxiliary data.");
                }
                else
                {
                    if (!shouldReload && Initialized)
                    {
                        return;
                    }

                    _logger.LogInformation("Starting the reload of auxiliary data.");

                    var stopwatch = Stopwatch.StartNew();

                    // Load the auxiliary files in parallel.
                    const string downloadsName = nameof(_data.Downloads);
                    const string verifiedPackagesName = nameof(_data.VerifiedPackages);
                    const string popularityTransfersName = nameof(_data.PopularityTransfers);
                    var downloadsTask = LoadAsync(_data?.Downloads, _downloadDataClient.ReadLatestIndexedAsync);
                    var verifiedPackagesTask = LoadAsync(_data?.VerifiedPackages, _verifiedPackagesDataClient.ReadLatestAsync);
                    var popularityTransfersTask = LoadAsync(_data?.PopularityTransfers, _popularityTransferDataClient.ReadLatestIndexedAsync);
                    await Task.WhenAll(downloadsTask, verifiedPackagesTask);
                    var downloads = await downloadsTask;
                    var verifiedPackages = await verifiedPackagesTask;
                    var popularityTransfers = await popularityTransfersTask;

                    // Keep track of what was actually reloaded and what didn't change.
                    var reloadedNames = new List<string>();
                    var notModifiedNames = new List<string>();
                    (ReferenceEquals(_data?.Downloads, downloads) ? notModifiedNames : reloadedNames).Add(downloadsName);
                    (ReferenceEquals(_data?.VerifiedPackages, verifiedPackages) ? notModifiedNames : reloadedNames).Add(verifiedPackagesName);
                    (ReferenceEquals(_data?.PopularityTransfers, popularityTransfers) ? notModifiedNames : reloadedNames).Add(popularityTransfersName);

                    // Reference assignment is atomic, so this is what makes the data available for readers.
                    _data = new AuxiliaryData(
                        DateTimeOffset.UtcNow,
                        downloads,
                        verifiedPackages,
                        popularityTransfers);

                    // Track the counts regarding the string cache status.
                    _telemetryService.TrackAuxiliaryFilesStringCache(
                        _stringCache.StringCount,
                        _stringCache.CharCount,
                        _stringCache.RequestCount,
                        _stringCache.HitCount);
                    _stringCache.ResetCounts();

                    stopwatch.Stop();
                    _telemetryService.TrackAuxiliaryFilesReload(reloadedNames, notModifiedNames, stopwatch.Elapsed);
                    _logger.LogInformation(
                        "Done reloading auxiliary data. Took {Duration}. Reloaded: {Reloaded}. Not modified: {NotModified}",
                        stopwatch.Elapsed,
                        reloadedNames,
                        notModifiedNames);
                }
            }
            finally
            {
                if (acquired)
                {
                    _lock.Release();
                }
            }
        }

        private async Task<AuxiliaryFileResult<T>> LoadAsync<T>(
            AuxiliaryFileResult<T> previousResult,
            Func<IAccessCondition, StringCache, Task<AuxiliaryFileResult<T>>> getResult) where T : class
        {
            await Task.Yield();

            IAccessCondition accessCondition;
            if (previousResult == null)
            {
                accessCondition = AccessConditionWrapper.GenerateEmptyCondition();
            }
            else
            {
                accessCondition = AccessConditionWrapper.GenerateIfNoneMatchCondition(previousResult.Metadata.ETag);
            }

            var newResult = await getResult(accessCondition, _stringCache);
            if (newResult.Modified)
            {
                return newResult;
            }
            else
            {
                return previousResult;
            }
        }

        public IAuxiliaryData Get()
        {
            if (_data == null)
            {
                throw new InvalidOperationException(
                    $"The auxiliary data has not been loaded yet. Call {nameof(LoadAsync)}.");
            }

            return _data;
        }
    }
}
