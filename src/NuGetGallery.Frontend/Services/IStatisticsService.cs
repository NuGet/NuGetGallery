
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

        Task<bool> LoadDownloadPackages();
        Task<bool> LoadDownloadPackageVersions();
        Task<bool> LoadNuGetClientVersion();
        Task<bool> LoadLast6Months();

        Task<StatisticsPackagesReport> GetPackageDownloadsByVersion(string packageId);
        Task<StatisticsPackagesReport> GetPackageVersionDownloadsByClient(string packageId, string packageVersion);
    }
}
