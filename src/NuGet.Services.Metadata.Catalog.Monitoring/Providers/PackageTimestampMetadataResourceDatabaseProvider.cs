// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageTimestampMetadataResourceDatabaseProvider : ResourceProvider
    {
        private readonly IGalleryDatabaseQueryService _galleryDatabase;

        public PackageTimestampMetadataResourceDatabaseProvider(
            IGalleryDatabaseQueryService galleryDatabaseQueryService,
            ILoggerFactory loggerFactory)
            : base(typeof(IPackageTimestampMetadataResource))
        {
            _galleryDatabase = galleryDatabaseQueryService ?? throw new ArgumentNullException(nameof(galleryDatabaseQueryService));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        private readonly ILoggerFactory _loggerFactory;

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PackageTimestampMetadataResourceDatabase resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)
            {
                resource = new PackageTimestampMetadataResourceDatabase(
                    _galleryDatabase,
                    _loggerFactory.CreateLogger<PackageTimestampMetadataResourceDatabase>());
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}