// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageTimestampMetadataResourceV2 : IPackageTimestampMetadataResource
    {
        public PackageTimestampMetadataResourceV2(
            string source,
            ILogger<PackageTimestampMetadataResourceV2> logger)
        {
            _source = source;
            _logger = logger;
        }
        
        private readonly string _source;
        private readonly ILogger<PackageTimestampMetadataResourceV2> _logger;

        /// <summary>
        /// Parses the feed for the package specified by the <see cref="ValidationContext"/> and returns a <see cref="PackageTimestampMetadata"/>.
        /// If the package is missing from the feed, returns the package's deletion audit record timestamp.
        /// </summary>
        public async Task<PackageTimestampMetadata> GetAsync(ValidationContext data)
        {
            var feedPackageDetails = await FeedHelpers.GetPackage(data.Client, _source, data.Package.Id,
                data.Package.Version.ToString());

            if (feedPackageDetails != null)
            {
                return PackageTimestampMetadata.CreateForPackageExistingOnFeed(feedPackageDetails.CreatedDate, feedPackageDetails.LastEditedDate);
            }

            DateTime? deleted = null;
            if (data.DeletionAuditEntries.Any())
            {
                deleted = data.DeletionAuditEntries.Max(entry => entry.TimestampUtc);
            }

            return PackageTimestampMetadata.CreateForPackageMissingFromFeed(deleted);
        }
    }
}
