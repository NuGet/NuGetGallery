// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Configuration
{
    /// <summary>
    /// Settings concerning when users can delete their own packages. This configuration does not effect the admin
    /// package delete flow.
    /// </summary>
    public interface IPackageDeleteConfiguration
    {
        /// <summary>
        /// Whether or not users can delete their new packages through the "Contact Support" flow.
        /// </summary>
        bool AllowUsersToDeletePackages { get; }

        /// <summary>
        /// If the package ID has more than this many downloads, the user cannot delete new versions no matter what.
        /// The <see cref="AllowUsersToDeletePackages"/> option takes precedence over this option. If this value is
        /// null, this download count restriction does not apply.
        /// </summary>
        int? MaximumDownloadsForPackageId { get; }

        /// <summary>
        /// How frequently download statistics are updated in hours. This value may not be exact, but is used as an
        /// "early" delete criteria. If a package was pushed less than this many hours ago, the package can be deleted
        /// regardless of the number of downloads. Since the download counts for packages are not updated immediately,
        /// there is a window of time when a new package won't have any recorded downloads. Note that
        /// <see cref="AllowUsersToDeletePackages"/> and <see cref="MaximumDownloadsForPackageVersion"/> take
        /// precedence over this option. The jobs that imply this value are the Stats.CreateAzureCdnWarehouseReports and
        /// Stats.AggregateCdnDownloadsInGallery jobs. Note that this configuration value may be higher than the actual
        /// update frequency. This is the more like the maximum expected update frequency.
        /// </summary>
        int? StatisticsUpdateFrequencyInHours { get; }

        /// <summary>
        /// The hour threshold for the "late" delete criteria. If a package was pushed less than this many hours ago
        /// and if the package version has less than <see cref="MaximumDownloadsForPackageVersion"/> downloads, the
        /// package can be deleted. If this value is null, the package cannot be deleted after
        /// <see cref="StatisticsUpdateFrequencyInHours"/> hours. Also, if the package has not had its download
        /// statistics updated in the last <see cref="StatisticsUpdateFrequencyInHours"/> hours, the delete is not
        /// allowed since the download data is too stale. If <see cref="StatisticsUpdateFrequencyInHours"/> is null,
        /// download statistics are never considered stale.
        /// </summary>
        int? HourLimitWithMaximumDownloads { get; }

        /// <summary>
        /// If the number of downloads on a package is greater than this value, the package was published less than
        /// <see cref="StatisticsUpdateFrequencyInHours"/> hours ago, and the package was downloaded more than
        /// <see cref="StatisticsUpdateFrequencyInHours"/> hours ago, the package can be deleted. If this value is
        /// null, this download count restriction is not applied.
        /// </summary>
        int? MaximumDownloadsForPackageVersion { get; }
    }
}