
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

    public class NullStatisticsService : IStatisticsService
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "Type is immutable")]
        public static readonly NullStatisticsService Instance = new NullStatisticsService();

        private NullStatisticsService() { }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary
        {
            get { return Enumerable.Empty<StatisticsPackagesItemViewModel>(); }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        {
            get { return Enumerable.Empty<StatisticsPackagesItemViewModel>(); }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        {
            get { return Enumerable.Empty<StatisticsPackagesItemViewModel>(); }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        {
            get { return Enumerable.Empty<StatisticsPackagesItemViewModel>(); }
        }

        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion
        {
            get { return Enumerable.Empty<StatisticsNuGetUsageItem>(); }
        }

        public IEnumerable<StatisticsMonthlyUsageItem> Last6Months
        {
            get { return Enumerable.Empty<StatisticsMonthlyUsageItem>(); }
        }

        public Task<StatisticsReportResult> LoadDownloadPackages()
        {
            return Task.FromResult(StatisticsReportResult.Failed);
        }

        public Task<StatisticsReportResult> LoadDownloadPackageVersions()
        {
            return Task.FromResult(StatisticsReportResult.Failed);
        }

        public Task<StatisticsReportResult> LoadNuGetClientVersion()
        {
            return Task.FromResult(StatisticsReportResult.Failed);
        }

        public Task<StatisticsReportResult> LoadLast6Months()
        {
            return Task.FromResult(StatisticsReportResult.Failed);
        }

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
