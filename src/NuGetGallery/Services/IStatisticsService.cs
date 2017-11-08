// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IStatisticsService
    {
        StatisticsReportResult PackageDownloadsResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> PackageDownloads { get; }
        IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsSummary { get; }

        StatisticsReportResult PackageVersionDownloadsResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> PackageVersionDownloads { get; }
        IEnumerable<StatisticsPackagesItemViewModel> PackageVersionDownloadsSummary { get; }

        StatisticsReportResult CommunityPackageDownloadsResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageDownloads { get; }
        IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageDownloadsSummary { get; }

        StatisticsReportResult CommunityPackageVersionDownloadsResult { get; }
        IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageVersionDownloads { get; }
        IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageVersionDownloadsSummary { get; }

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