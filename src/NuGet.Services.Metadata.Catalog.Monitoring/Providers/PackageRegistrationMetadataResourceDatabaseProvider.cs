// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageRegistrationMetadataResourceDatabaseFeedProvider : ResourceProvider
    {
        private readonly IGalleryDatabaseQueryService _queryService;

        public PackageRegistrationMetadataResourceDatabaseFeedProvider(
            IGalleryDatabaseQueryService queryService) :
            base(typeof(IPackageRegistrationMetadataResource),
                nameof(IPackageRegistrationMetadataResource))
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            IPackageRegistrationMetadataResource resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)
            {
                resource = new PackageRegistrationMetadataResourceDatabase(_queryService);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
