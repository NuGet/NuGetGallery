
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

        Task<bool> LoadDownloadPackages();
        Task<bool> LoadDownloadPackageVersions();

        Task<StatisticsPackagesReport> GetPackageDownloadsByVersion(string packageId);
        Task<StatisticsPackagesReport> GetPackageVersionDownloadsByClient(string packageId, string packageVersion);
    }
}
