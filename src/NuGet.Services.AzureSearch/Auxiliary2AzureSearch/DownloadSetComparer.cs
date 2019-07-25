// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class DownloadSetComparer : IDownloadSetComparer
    {
        /// <summary>
        /// If there are more than this number of packages (ID + version) that have their download count decrease,
        /// throw an exception. This indicates a data issue and we should proceed no further.
        /// </summary>
        private const int MaxDecreases = 5000;

        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<DownloadSetComparer> _logger;

        public DownloadSetComparer(
            IAzureSearchTelemetryService telemetryService,
            ILogger<DownloadSetComparer> logger)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SortedDictionary<string, long> Compare(
            DownloadData oldData,
            DownloadData newData)
        {
            if (newData.Count == 0)
            {
                throw new InvalidOperationException("The new data should not be empty.");
            }

            var stopwatch = Stopwatch.StartNew();

            // We use a very simplistic algorithm here. Find the union of both ID sets and compare each download count.
            var uniqueIds = new HashSet<string>(
                oldData.Keys.Concat(newData.Keys),
                StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation(
                "There are {OldCount} IDs in the old data, {NewCount} IDs in the new data, and {TotalCount} IDs in total.",
                oldData.Count,
                newData.Count,
                uniqueIds.Count);

            var result = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var decreaseCount = 0;
            foreach (var id in uniqueIds)
            {
                // Detect download count decreases and emit a metric. This is not necessarily wrong because there have
                // been times that we manually delete spoofed download counts.
                DetectDownloadCountDecreases(oldData, newData, id, ref decreaseCount);

                var oldCount = oldData.GetDownloadCount(id);
                var newCount = newData.GetDownloadCount(id);
                if (oldCount != newCount)
                {
                    result.Add(id, newCount);
                }
            }

            _logger.LogInformation("There are {Count} package IDs with download count changes.", result.Count);

            stopwatch.Stop();
            _telemetryService.TrackDownloadSetComparison(oldData.Count, newData.Count, result.Count, stopwatch.Elapsed);

            return result;
        }

        private void DetectDownloadCountDecreases(DownloadData oldData, DownloadData newData, string id, ref int decreaseCount)
        {
            var oldHasId = oldData.TryGetValue(id, out var oldDownloads);
            if (!oldHasId)
            {
                oldDownloads = new DownloadByVersionData();
            }

            var newHasId = newData.TryGetValue(id, out var newDownloads);
            if (!newHasId)
            {
                newDownloads = new DownloadByVersionData();
            }

            var uniqueVersions = new HashSet<string>(
                oldDownloads.Keys.Concat(newDownloads.Keys),
                StringComparer.OrdinalIgnoreCase);

            foreach (var version in uniqueVersions)
            {
                var oldHasVersion = oldDownloads.TryGetValue(version, out var oldCount);
                var newHasVersion = newDownloads.TryGetValue(version, out var newCount);

                if (newCount < oldCount)
                {
                    decreaseCount++;

                    if (decreaseCount > MaxDecreases)
                    {
                        throw new InvalidOperationException("Too many download count decreases are occurring.");
                    }

                    _telemetryService.TrackDownloadCountDecrease(
                        id,
                        version,
                        oldHasId,
                        oldHasVersion,
                        oldCount,
                        newHasId,
                        newHasVersion,
                        newCount);
                }
            }
        }
    }
}

