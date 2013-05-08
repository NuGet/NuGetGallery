using System.Collections.Generic;
using Microsoft.Internal.Web.Utils;
using NuGetGallery.Statistics;

namespace NuGetGallery
{
    public class StatisticsSummaryViewModel
    {
        public PackageDownloadsReport PackageDownloads { get; private set; }
        public PackageDownloadsReport PackageVersionDownloads { get; private set; }

        public StatisticsSummaryViewModel(PackageDownloadsReport packageDownloads, PackageDownloadsReport packageVersionDownloads)
        {
            PackageDownloads = packageDownloads;
            PackageVersionDownloads = packageVersionDownloads;
        }

        // Equals makes testing easier!! It's also just a Good Thing
        public override bool Equals(object obj)
        {
            StatisticsSummaryViewModel other = obj as StatisticsSummaryViewModel;
            return other != null &&
                Equals(PackageDownloads, other.PackageDownloads) &&
                Equals(PackageVersionDownloads, other.PackageVersionDownloads);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(PackageDownloads)
                .Add(PackageVersionDownloads)
                .CombinedHash;
        }

        //public StatisticsPackagesViewModel()
        //{
        //}

        //public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary 
        //{
        //    get; set;
        //}

        //public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        //{
        //    get; set;
        //}

        //public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        //{
        //    get; set; 
        //}

        //public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        //{
        //    get; set;
        //}

        //public StatisticsPackagesReport Report
        //{
        //    get;
        //    private set;
        //}

        //public bool IsReportAvailable { get { return (Report != null); } }

        //public string PackageId { get; private set; }
        //public string PackageVersion { get; private set; }

        //public void SetPackageDownloadsByVersion(string packageId, StatisticsPackagesReport report)
        //{
        //    PackageId = packageId;
        //    Report = report;
        //}

        //public void SetPackageVersionDownloadsByClient(string packageId, string packageVersion, StatisticsPackagesReport report)
        //{
        //    PackageId = packageId;
        //    PackageVersion = packageVersion;
        //    Report = report;
        //}
    }
}
