// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageRegistrationMetadataResourceDatabase : IPackageRegistrationMetadataResource
    {
        private readonly IGalleryDatabaseQueryService _galleryDatabase;

        public PackageRegistrationMetadataResourceDatabase(
            IGalleryDatabaseQueryService galleryDatabase)
        {
            _galleryDatabase = galleryDatabase ?? throw new ArgumentNullException(nameof(galleryDatabase));
        }

        /// <summary>
        /// Returns a <see cref="PackageRegistrationIndexMetadata"/> that represents how a package appears in the database.
        /// </summary>
        public async Task<PackageRegistrationIndexMetadata> GetIndexAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            try
            {
                var feedPackage = await GetPackageAsync(package);
                return feedPackage != null ? new PackageRegistrationIndexMetadata(feedPackage) : null;
            }
            catch (Exception e)
            {
                throw new ValidationException($"Could not fetch {nameof(PackageRegistrationIndexMetadata)} from database!", e);
            }
        }

        /// <summary>
        /// Returns a <see cref="PackageRegistrationIndexMetadata"/> that represents how a package appears in the database.
        /// </summary>
        public async Task<PackageRegistrationLeafMetadata> GetLeafAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            try
            {
                var feedPackage = await GetPackageAsync(package);
                return feedPackage != null ? new PackageRegistrationLeafMetadata(feedPackage) : null;
            }
            catch (Exception e)
            {
                throw new ValidationException($"Could not fetch {nameof(PackageRegistrationLeafMetadata)} from database!", e);
            }
        }

        private Task<FeedPackageDetails> GetPackageAsync(PackageIdentity package)
        {
            return _galleryDatabase.GetPackageOrNull(package.Id, package.Version.ToNormalizedString());
        }
    }
}
