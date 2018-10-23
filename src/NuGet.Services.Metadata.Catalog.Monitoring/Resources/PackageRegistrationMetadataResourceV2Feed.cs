// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageRegistrationMetadataResourceV2Feed : IPackageRegistrationMetadataResource
    {
        private V2FeedParser _feedParser;

        public PackageRegistrationMetadataResourceV2Feed(
            V2FeedParser feedParser)
        {
            _feedParser = feedParser;
        }

        /// <summary>
        /// Returns a <see cref="PackageRegistrationIndexMetadata"/> that represents how a package appears in V2's FindPackagesById.
        /// </summary>
        public async Task<PackageRegistrationIndexMetadata> GetIndexAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            try
            {
                var feedPackage = await GetPackageFromIndexAsync(package, log, token);
                return feedPackage != null ? new PackageRegistrationIndexMetadata(feedPackage) : null;
            }
            catch (Exception e)
            {
                throw new ValidationException($"Could not fetch {nameof(PackageRegistrationIndexMetadata)} from V2 feed!", e);
            }
        }

        /// <summary>
        /// Returns a <see cref="PackageRegistrationIndexMetadata"/> that represents how a package appears in V2's specific package endpoint (e.g. Packages(Id='...',Version='...')).
        /// </summary>
        public async Task<PackageRegistrationLeafMetadata> GetLeafAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            try
            {
                var feedPackage = await GetPackageFromLeafAsync(package, log, token);
                return feedPackage != null ? new PackageRegistrationLeafMetadata(feedPackage) : null;
            }
            catch (Exception e)
            {
                throw new ValidationException($"Could not fetch {nameof(PackageRegistrationLeafMetadata)} from V2 feed!", e);
            }
        }

        private async Task<V2FeedPackageInfo> GetPackageFromIndexAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            // If the package is missing from FindPackagesById, this will return null.
            var feedPackages = await _feedParser.FindPackagesByIdAsync(package.Id, NullSourceCacheContext.Instance, log, token);
            return feedPackages.FirstOrDefault(p => p.Version == package.Version);
        }

        private Task<V2FeedPackageInfo> GetPackageFromLeafAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            // If the package is missing from Packages(Id='...',Version='...'), this will return null.
            return _feedParser.GetPackage(package, NullSourceCacheContext.Instance, log, token);
        }
    }
}
