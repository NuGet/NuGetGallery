// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum StatisticsReportName
    {
        /// <summary>
        /// Most frequently downloaded package registration in last 6 weeks.
        /// </summary>
        RecentPopularity,
        /// <summary>
        /// Most frequently downloaded package, specific to actual version.
        /// </summary>
        RecentPopularityDetail,
        /// <summary>
        /// Breakout by version for a package (drill down from RecentPopularity).
        /// </summary>
        RecentPopularityDetail_,
        /// <summary>
        /// Most frequently downloaded community package registration in last 6 weeks.
        /// This excludes Microsoft and ASP.NET packages.
        /// </summary>
        RecentCommunityPopularity,
        /// <summary>
        /// Most frequently downloaded community package, specific to actual version.
        /// This excludes Microsoft and ASP.NET packages.
        /// </summary>
        RecentCommunityPopularityDetail,
        /// <summary>
        /// Downloads that have been done by the various NuGet client versions.
        /// </summary>
        NuGetClientVersion,
        /// <summary>
        /// Downloads per month.
        /// </summary>
        Last6Weeks
    };
}