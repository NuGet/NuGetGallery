// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Part of the metadata of a <see cref="PackageMonitoringStatus"/> as returned by <see cref="IPackageMonitoringStatusService.ListAsync(System.Threading.CancellationToken)"/>.
    /// The full <see cref="PackageMonitoringStatus"/> can be returned by calling <see cref="IPackageMonitoringStatusService.GetAsync(FeedPackageIdentity, System.Threading.CancellationToken)"/>.
    /// </summary>
    public class PackageMonitoringStatusListItem
    {
        public FeedPackageIdentity Package { get; }

        public PackageState State { get; }

        public PackageMonitoringStatusListItem(FeedPackageIdentity package, PackageState state)
        {
            Package = package;
            State = state;
        }
    }
}