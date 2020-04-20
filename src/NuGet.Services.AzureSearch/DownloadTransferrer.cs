// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch
{
    public class DownloadTransferrer : IDownloadTransferrer
    {
        private readonly ILogger<DownloadTransferrer> _logger;

        public DownloadTransferrer(ILogger<DownloadTransferrer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SortedDictionary<string, long> InitializeDownloadTransfers(
            DownloadData downloads,
            SortedDictionary<string, SortedSet<string>> outgoingTransfers,
            IReadOnlyDictionary<string, long> downloadOverrides)
        {
            // TODO: Add download changes due to popularity transfers.
            // See: https://github.com/NuGet/NuGetGallery/issues/7898
            var downloadTransfers = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            // TODO: Remove download overrides.
            // See: https://github.com/NuGet/Engineering/issues/3089
            ApplyDownloadOverrides(downloads, downloadOverrides, downloadTransfers);

            return downloadTransfers;
        }

        public SortedDictionary<string, long> UpdateDownloadTransfers(
            DownloadData downloads,
            SortedDictionary<string, long> downloadChanges,
            SortedDictionary<string, SortedSet<string>> oldTransfers,
            SortedDictionary<string, SortedSet<string>> newTransfers,
            IReadOnlyDictionary<string, long> downloadOverrides)
        {
            Guard.Assert(
                downloadChanges.Comparer == StringComparer.OrdinalIgnoreCase,
                $"Download changes should have comparer {nameof(StringComparer.OrdinalIgnoreCase)}");

            Guard.Assert(
                oldTransfers.Comparer == StringComparer.OrdinalIgnoreCase,
                $"Old popularity transfer should have comparer {nameof(StringComparer.OrdinalIgnoreCase)}");

            Guard.Assert(
                downloadChanges.All(x => downloads.GetDownloadCount(x.Key) == x.Value),
                "The download changes should match the latest downloads");

            // TODO: Add download changes due to popularity transfers.
            // See: https://github.com/NuGet/NuGetGallery/issues/7898
            var downloadTransfers = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            // TODO: Remove download overrides.
            // See: https://github.com/NuGet/Engineering/issues/3089
            ApplyDownloadOverrides(downloads, downloadOverrides, downloadTransfers);

            return downloadTransfers;
        }

        private void ApplyDownloadOverrides(
            DownloadData downloads,
            IReadOnlyDictionary<string, long> downloadOverrides,
            SortedDictionary<string, long> transferredDownloads)
        {
            // TODO: Remove download overrides.
            // See: https://github.com/NuGet/Engineering/issues/3089
            foreach (var downloadOverride in downloadOverrides)
            {
                var packageId = downloadOverride.Key;
                var packageDownloads = downloads.GetDownloadCount(packageId);

                if (transferredDownloads.TryGetValue(packageId, out var updatedDownloads))
                {
                    packageDownloads = updatedDownloads;
                }

                if (packageDownloads >= downloadOverride.Value)
                {
                    _logger.LogInformation(
                        "Skipping download override for package {PackageId} as its downloads of {Downloads} are " +
                        "greater than its override of {DownloadsOverride}",
                        packageId,
                        packageDownloads,
                        downloadOverride.Value);
                    continue;
                }

                _logger.LogInformation(
                    "Overriding downloads of package {PackageId} from {Downloads} to {DownloadsOverride}",
                    packageId,
                    packageDownloads,
                    downloadOverride.Value);

                transferredDownloads[packageId] = downloadOverride.Value;
            }
        }
    }
}