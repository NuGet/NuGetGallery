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
    /// Helper methods for writing ID-level (package registration) metadata to the catalog.
    /// This is the counterpart to <see cref="CatalogWriterHelper"/>, which writes per-version
    /// package metadata. The registration lane emits exactly one version-less
    /// <see cref="PackageSponsorshipDetailsCatalogItem"/> per changed package ID and advances the
    /// <c>nuget:lastRegistrationEdited</c> cursor.
    /// </summary>
    public static class RegistrationCatalogWriterHelper
    {
        /// <summary>
        /// Asynchronously writes ID-level sponsorship metadata to the catalog.
        /// </summary>
        /// <param name="registrations">The changed package registrations, keyed by their
        /// <c>RegistrationLastEdited</c> timestamp.</param>
        /// <param name="storage">Storage.</param>
        /// <param name="lastCreated">The catalog's last created cursor (preserved unchanged).</param>
        /// <param name="lastEdited">The catalog's last edited cursor (preserved unchanged).</param>
        /// <param name="lastDeleted">The catalog's last deleted cursor (preserved unchanged).</param>
        /// <param name="lastRegistrationEdited">The catalog's last registration edited cursor. This is
        /// advanced to the newest processed timestamp in the batch.</param>
        /// <param name="context">The catalog context.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="telemetryService">A telemetry service.</param>
        /// <param name="logger">A logger.</param>
        /// <returns>A task that represents the asynchronous operation. The task result returns the newest
        /// <see cref="DateTime"/> that was processed (the new <c>lastRegistrationEdited</c> cursor value).</returns>
        public static async Task<DateTime> WritePackageSponsorshipDetailsToCatalogAsync(
            SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>> registrations,
            IStorage storage,
            DateTime lastCreated,
            DateTime lastEdited,
            DateTime lastDeleted,
            DateTime lastRegistrationEdited,
            CatalogContext context,
            CancellationToken cancellationToken,
            ITelemetryService telemetryService,
            ILogger logger)
        {
            if (registrations == null)
            {
                throw new ArgumentNullException(nameof(registrations));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (registrations.Count == 0)
            {
                return lastRegistrationEdited;
            }

            var writer = new AppendOnlyCatalogWriter(storage, telemetryService, context: context);

            // Flatten the sorted list, preserving the ascending timestamp order so the newest leaf per
            // package ID is written last (and therefore wins on the registration side-lane).
            var items = registrations.SelectMany(pair => pair.Value);

            // AppendOnlyCatalogWriter.Add(...) is not thread-safe, so add them all on one thread.
            foreach (var registration in items)
            {
                var catalogItem = new PackageSponsorshipDetailsCatalogItem(
                    registration.PackageId,
                    registration.SponsorshipUrls);

                writer.Add(catalogItem);

                logger?.LogInformation(
                    "Add ID-level sponsorship metadata for: {PackageId}",
                    registration.PackageId);
            }

            var newLastRegistrationEdited = registrations.Last().Key;

            var commitMetadata = PackageCatalog.CreateCommitMetadata(
                writer.RootUri,
                new CommitMetadata(lastCreated, lastEdited, lastDeleted, newLastRegistrationEdited));

            await writer.Commit(commitMetadata, cancellationToken);

            logger?.LogInformation("COMMIT ID-level sponsorship metadata to catalog.");

            return newLastRegistrationEdited;
        }
    }
}
