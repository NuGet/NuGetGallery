// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Monitoring
{
    /// <summary>
    /// Fetches <see cref="PackageStatusOutdatedCheck"/> from <see cref="IGalleryDatabaseQueryService.GetPackagesEditedSince(DateTime, int)"/>.
    /// The <see cref="PackageStatusOutdatedCheck"/>s fetched represent the latest state for existing packages.
    /// </summary>
    public class DatabasePackageStatusOutdatedCheckSource : PackageStatusOutdatedCheckSource<FeedPackageDetails>
    {
        private readonly IGalleryDatabaseQueryService _galleryDatabaseQueryService;

        public DatabasePackageStatusOutdatedCheckSource(
            ReadWriteCursor cursor,
            IGalleryDatabaseQueryService galleryDatabase)
            : base(cursor)
        {
            _galleryDatabaseQueryService = galleryDatabase ?? throw new ArgumentNullException(nameof(galleryDatabase));
        }

        protected override DateTime GetCursorValue(FeedPackageDetails package)
        {
            return package.LastEditedDate;
        }

        protected override PackageStatusOutdatedCheck GetPackageStatusOutdatedCheck(FeedPackageDetails package)
        {
            return new PackageStatusOutdatedCheck(package);
        }

        /// <remarks>
        /// Any values greater than <see cref="Constants.MaxPageSize"/> will be ignored by <see cref="IGalleryDatabaseQueryService"/>.
        /// </remarks>
        protected override async Task<IReadOnlyCollection<FeedPackageDetails>> GetPackagesToCheckAsync(DateTime since, DateTime max, int top, CancellationToken cancellationToken)
        {
            return (await _galleryDatabaseQueryService.GetPackagesEditedSince(since, top))
                .SelectMany(p => p.Value)
                .Where(p => p.LastEditedDate < max)
                .ToList();
        }
    }
}
