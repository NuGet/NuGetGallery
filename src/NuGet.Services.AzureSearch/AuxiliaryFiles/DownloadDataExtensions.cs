// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public static class DownloadDataExtensions
    {
        public static DownloadData ApplyDownloadOverrides(
            this DownloadData originalData,
            IReadOnlyDictionary<string, long> downloadOverrides,
            ILogger logger)
        {
            if (originalData == null)
            {
                throw new ArgumentNullException(nameof(originalData));
            }

            if (downloadOverrides == null)
            {
                throw new ArgumentNullException(nameof(downloadOverrides));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // Create a copy of the original data and apply overrides as we copy.
            var result = new DownloadData();

            foreach (var downloadData in originalData)
            {
                var packageId = downloadData.Key;

                if (ShouldOverrideDownloads(packageId))
                {
                    logger.LogInformation(
                        "Overriding downloads of package {PackageId} from {Downloads} to {DownloadsOverride}",
                        packageId,
                        originalData.GetDownloadCount(packageId),
                        downloadOverrides[packageId]);

                    var versions = downloadData.Value.Keys;

                    result.SetDownloadCount(
                        packageId,
                        versions.First(),
                        downloadOverrides[packageId]);
                }
                else
                {
                    foreach (var versionData in downloadData.Value)
                    {
                        result.SetDownloadCount(downloadData.Key, versionData.Key, versionData.Value);
                    }
                }
            }

            bool ShouldOverrideDownloads(string packageId)
            {
                if (!downloadOverrides.TryGetValue(packageId, out var downloadOverride))
                {
                    return false;
                }

                // Apply the downloads override only if the package has fewer total downloads.
                // In effect, this removes a package's manual boost once its total downloads exceed the override.
                if (originalData[packageId].Total >= downloadOverride)
                {
                    logger.LogInformation(
                        "Skipping download override for package {PackageId} as its downloads of {Downloads} are " +
                        "greater than its override of {DownloadsOverride}",
                        packageId,
                        originalData[packageId].Total,
                        downloadOverride);
                    return false;
                }

                return true;
            }

            return result;
        }
    }
}
