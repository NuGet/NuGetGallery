// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Helper methods for writing to the catalog.
    /// </summary>
    public static class CatalogWriterHelper
    {
        /// <summary>
        /// Asynchronously writes package metadata to the catalog.
        /// </summary>
        /// <param name="packageCatalogItemCreator">A package catalog item creator.</param>
        /// <param name="packages">Packages to download metadata for.</param>
        /// <param name="storage">Storage.</param>
        /// <param name="lastCreated">The catalog's last created datetime.</param>
        /// <param name="lastEdited">The catalog's last edited datetime.</param>
        /// <param name="lastDeleted">The catalog's last deleted datetime.</param>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism for package processing.</param>
        /// <param name="createdPackages"><c>true</c> to include created packages; otherwise, <c>false</c>.</param>
        /// <param name="updateCreatedFromEdited"><c>true</c> to update the created cursor from the last edited cursor;
        /// otherwise, <c>false</c>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="telemetryService">A telemetry service.</param>
        /// <param name="logger">A logger.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns the latest
        /// <see cref="DateTime}" /> that was processed.</returns>
        public static async Task<DateTime> WritePackageDetailsToCatalogAsync(
            IPackageCatalogItemCreator packageCatalogItemCreator,
            SortedList<DateTime, IList<FeedPackageDetails>> packages,
            IStorage storage,
            DateTime lastCreated,
            DateTime lastEdited,
            DateTime lastDeleted,
            int maxDegreeOfParallelism,
            bool? createdPackages,
            bool updateCreatedFromEdited,
            CatalogContext context,
            CancellationToken cancellationToken,
            ITelemetryService telemetryService,
            ILogger logger)
        {
            if (packageCatalogItemCreator == null)
            {
                throw new ArgumentNullException(nameof(packageCatalogItemCreator));
            }

            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDegreeOfParallelism),
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var writer = new AppendOnlyCatalogWriter(storage, telemetryService, context: context);

            var lastDate = DetermineLastDate(lastCreated, lastEdited, createdPackages);

            if (packages.Count == 0)
            {
                return lastDate;
            }

            // Flatten the sorted list.
            var workItems = packages.SelectMany(
                    pair => pair.Value.Select(
                        details => new PackageWorkItem(pair.Key, details)))
                .ToArray();

            await workItems.ForEachAsync(maxDegreeOfParallelism, async workItem =>
            {
                workItem.PackageCatalogItem = await packageCatalogItemCreator.CreateAsync(
                    workItem.FeedPackageDetails,
                    workItem.Timestamp,
                    cancellationToken);
            });

            lastDate = packages.Last().Key;

            // AppendOnlyCatalogWriter.Add(...) is not thread-safe, so add them all at once on one thread.
            foreach (var workItem in workItems.Where(workItem => workItem.PackageCatalogItem != null))
            {
                writer.Add(workItem.PackageCatalogItem);

                logger?.LogInformation("Add metadata from: {PackageDetailsContentUri}", workItem.FeedPackageDetails.ContentUri);
            }

            if (createdPackages.HasValue)
            {
                lastEdited = !createdPackages.Value ? lastDate : lastEdited;

                if (updateCreatedFromEdited)
                {
                    lastCreated = lastEdited;
                }
                else
                {
                    lastCreated = createdPackages.Value ? lastDate : lastCreated;
                }
            }

            var commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, new CommitMetadata(lastCreated, lastEdited, lastDeleted));

            await writer.Commit(commitMetadata, cancellationToken);

            logger?.LogInformation("COMMIT metadata to catalog.");

            return lastDate;
        }

        private static DateTime DetermineLastDate(DateTime lastCreated, DateTime lastEdited, bool? createdPackages)
        {
            if (!createdPackages.HasValue)
            {
                return DateTime.MinValue;
            }
            return createdPackages.Value ? lastCreated : lastEdited;
        }

        private sealed class PackageWorkItem
        {
            internal DateTime Timestamp { get; }
            internal FeedPackageDetails FeedPackageDetails { get; }
            internal PackageCatalogItem PackageCatalogItem { get; set; }

            internal PackageWorkItem(DateTime timestamp, FeedPackageDetails feedPackageDetails)
            {
                Timestamp = timestamp;
                FeedPackageDetails = feedPackageDetails;
            }
        }
    }
}