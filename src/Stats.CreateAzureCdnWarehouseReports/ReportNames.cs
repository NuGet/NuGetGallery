// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal static class ReportNames
    {
        public static string Extension = ".json";

        public static string[] StandardReports = new []
        {
            NuGetClientVersion,
            Last6Weeks,
            RecentCommunityPopularity,
            RecentCommunityPopularityDetail,
            RecentPopularity,
            RecentPopularityDetail
        };

        public static string[] AllReports = StandardReports.Concat(new[] {
            RecentPopularityDetailByPackageId,
            DownloadCount,
            GalleryTotals,
            DownloadsPerToolVersion
        }).ToArray();

        // Standard reports (ReportBuilder, ReportDataCollector)
        public const string NuGetClientVersion = "nugetclientversion";
        public const string Last6Weeks = "last6weeks";
        public const string RecentCommunityPopularity = "recentcommunitypopularity";
        public const string RecentCommunityPopularityDetail = "recentcommunitypopularitydetail";
        public const string RecentPopularity = "recentpopularity";
        public const string RecentPopularityDetail = "recentpopularitydetail";
        public const string RecentPopularityDetailByPackageId = "recentpopularitydetailbypackageid";

        // Custom reports (ReportBase)
        public const string DownloadCount = "downloads.v1";
        public const string GalleryTotals = "stats-totals";
        public const string DownloadsPerToolVersion = "tools.v1";
    }
}
