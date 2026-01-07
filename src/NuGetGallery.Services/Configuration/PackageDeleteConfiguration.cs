// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Configuration
{
    public class PackageDeleteConfiguration : IPackageDeleteConfiguration
    {
        public bool AllowUsersToDeletePackages { get; set; }
        public int? MaximumDownloadsForPackageId { get; set; }
        public int? StatisticsUpdateFrequencyInHours { get; set; }
        public int? HourLimitWithMaximumDownloads { get; set; }
        public int? MaximumDownloadsForPackageVersion { get; set; }
    }
}