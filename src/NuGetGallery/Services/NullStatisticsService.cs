// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class NullStatisticsService : IStatisticsService
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "Type is immutable")]
        public static readonly NullStatisticsService Instance = new NullStatisticsService();

        private NullStatisticsService() { }

        public StatisticsReportResult DownloadPackagesResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult DownloadPackageVersionsResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult DownloadCommunityPackagesResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesAll => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult DownloadCommunityPackageVersionsResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsAll => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult NuGetClientVersionResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion => Enumerable.Empty<StatisticsNuGetUsageItem>();

        public StatisticsReportResult Last6WeeksResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsWeeklyUsageItem> Last6Weeks => Enumerable.Empty<StatisticsWeeklyUsageItem>();

        public DateTime? LastUpdatedUtc => null;

        public Task Refresh() => Task.CompletedTask;

        public Task<StatisticsPackagesReport> GetPackageDownloadsByVersion(string packageId)
        {
            return Task.FromResult(new StatisticsPackagesReport());
        }

        public Task<StatisticsPackagesReport> GetPackageVersionDownloadsByClient(string packageId, string packageVersion)
        {
            return Task.FromResult(new StatisticsPackagesReport());
        }
    }
}