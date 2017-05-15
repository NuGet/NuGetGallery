// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;

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

        public async Task<PackageRegistrationIndexMetadata> GetIndex(PackageIdentity package, ILogger log, CancellationToken token)
        {
            var feedPackage = await GetPackageFromIndex(package, log, token);
            return feedPackage != null ? new PackageRegistrationIndexMetadata(feedPackage) : null;
        }

        public async Task<PackageRegistrationLeafMetadata> GetLeaf(PackageIdentity package, ILogger log, CancellationToken token)
        {
            var feedPackage = await GetPackageFromLeaf(package, log, token);
            return feedPackage != null ? new PackageRegistrationLeafMetadata(feedPackage) : null;
        }

        private async Task<V2FeedPackageInfo> GetPackageFromIndex(PackageIdentity package, ILogger log, CancellationToken token)
        {
            var feedPackages = await _feedParser.FindPackagesByIdAsync(package.Id, log, token);
            return feedPackages.FirstOrDefault(p => p.Version == package.Version);
        }

        private async Task<V2FeedPackageInfo> GetPackageFromLeaf(PackageIdentity package, ILogger log, CancellationToken token)
        {
            return await _feedParser.GetPackage(package, log, token);
        }
    }
}
