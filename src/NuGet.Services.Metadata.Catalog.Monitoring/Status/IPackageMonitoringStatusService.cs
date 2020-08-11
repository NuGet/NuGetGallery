// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Used to manage the status of packages that validation has ran against.
    /// </summary>
    public interface IPackageMonitoringStatusService
    {
        /// <summary>
        /// Returns a list of every package that has been monitored and its <see cref="PackageState"/>.
        /// </summary>
        Task<IEnumerable<PackageMonitoringStatusListItem>> ListAsync(CancellationToken token);

        /// <summary>
        /// Returns the validation status of a package.
        /// If validation has not yet been run on the package, returns null.
        /// </summary>
        Task<PackageMonitoringStatus> GetAsync(FeedPackageIdentity package, CancellationToken token);

        /// <summary>
        /// Returns the status of all packages that have a specified <see cref="PackageMonitoringStatus.State"/>.
        /// </summary>
        Task<IEnumerable<PackageMonitoringStatus>> GetAsync(PackageState type, CancellationToken token);

        /// <summary>
        /// Updates the status of a package.
        /// </summary>
        Task UpdateAsync(PackageMonitoringStatus status, CancellationToken token);
    }
}
