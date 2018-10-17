// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class NullStatisticsService : IStatisticsService
    {
        public static readonly NullStatisticsService Instance = new NullStatisticsService();

        private NullStatisticsService() { }

        public StatisticsReportResult PackageDownloadsResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloads => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult PackageVersionDownloadsResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> PackageVersionDownloads => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> PackageVersionDownloadsSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult CommunityPackageDownloadsResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageDownloads => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageDownloadsSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

        public StatisticsReportResult CommunityPackageVersionDownloadsResult => StatisticsReportResult.Failed;
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageVersionDownloads => Enumerable.Empty<StatisticsPackagesItemViewModel>();
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageVersionDownloadsSummary => Enumerable.Empty<StatisticsPackagesItemViewModel>();

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