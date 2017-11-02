// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IStatisticsService
    {
        StatisticsReportResult DownloadPackagesResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary { get; }

        StatisticsReportResult DownloadPackageVersionsResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary { get; }

        StatisticsReportResult DownloadCommunityPackagesResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesAll { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesSummary { get; }

        StatisticsReportResult DownloadCommunityPackageVersionsResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsAll { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsSummary { get; }

        StatisticsReportResult NuGetClientVersionResult { get; }
        IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion { get; }

        StatisticsReportResult Last6WeeksResult { get; }
        IEnumerable<StatisticsWeeklyUsageItem> Last6Weeks { get; }

        DateTime? LastUpdatedUtc { get; }
        Task Refresh();

        Task<StatisticsPackagesReport> GetPackageDownloadsByVersion(string packageId);
        Task<StatisticsPackagesReport> GetPackageVersionDownloadsByClient(string packageId, string packageVersion);
    }
}