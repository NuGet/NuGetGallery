// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Contains package statistics for a profile.
    /// </summary>
    public class ProfilePackageStatistics
    {
        /// <summary>
        /// The total number of (visible) packages the profile owns.
        /// </summary>
        public int TotalPackages { get; set; }

        /// <summary>
        /// The total number of times a (visible) package has been downloaded.
        /// </summary>
        public long TotalPackageDownloadCount { get; set; }
    }
}