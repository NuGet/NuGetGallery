// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageTimestampMetadataResourceDatabase : IPackageTimestampMetadataResource
    {
        private readonly IGalleryDatabaseQueryService _galleryDatabase;
        private readonly ILogger<PackageTimestampMetadataResourceDatabase> _logger;

        public PackageTimestampMetadataResourceDatabase(
            IGalleryDatabaseQueryService galleryDatabase,
            ILogger<PackageTimestampMetadataResourceDatabase> logger)
        {
            _galleryDatabase = galleryDatabase ?? throw new ArgumentNullException(nameof(galleryDatabase));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Queries the gallery database for the package specified by the <see cref="ValidationContext"/> and returns a <see cref="PackageTimestampMetadata"/>.
        /// If the package is missing from the repository, returns the package's deletion audit record timestamp.
        /// </summary>
        public async Task<PackageTimestampMetadata> GetAsync(ValidationContext context)
        {
            var feedPackageDetails = await _galleryDatabase.GetPackageOrNull(
                context.Package.Id,
                context.Package.Version.ToNormalizedString());

            if (feedPackageDetails != null)
            {
                return PackageTimestampMetadata.CreateForExistingPackage(
                    feedPackageDetails.CreatedDate,
                    feedPackageDetails.LastEditedDate);
            }

            DateTime? deleted = null;
            if (context.DeletionAuditEntries.Any())
            {
                deleted = context.DeletionAuditEntries.Max(entry => entry.TimestampUtc);
            }

            return PackageTimestampMetadata.CreateForMissingPackage(deleted);
        }
    }
}