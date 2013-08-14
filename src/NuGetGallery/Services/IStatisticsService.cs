
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IStatisticsService
    {
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll { get; }
        IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll { get; }
        IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion { get; }
        IEnumerable<StatisticsMonthlyUsageItem> Last6Months { get; }

        Task<StatisticsReportResult> LoadDownloadPackages();
        Task<StatisticsReportResult> LoadDownloadPackageVersions();
        Task<StatisticsReportResult> LoadNuGetClientVersion();
        Task<StatisticsReportResult> LoadLast6Months();

        Task<StatisticsPackagesReport> GetPackageDownloadsByVersion(string packageId);
        Task<StatisticsPackagesReport> GetPackageVersionDownloadsByClient(string packageId, string packageVersion);
    }
}
