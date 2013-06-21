using System.Collections.Generic;
using System.Globalization;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        public StatisticsPackagesViewModel()
        {
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary 
        {
            get; set;
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        {
            get; set;
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        {
            get; set; 
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        {
            get; set;
        }

        public CultureInfo ClientCulture { get; set; }

        public StatisticsPackagesReport Report
        {
            get;
            private set;
        }

        public bool IsDownloadPackageAvailable { get; set; }
        public bool IsDownloadPackageDetailAvailable { get; set; }

        public bool IsReportAvailable { get { return (Report != null); } }

        public string PackageId { get; private set; }
        public string PackageVersion { get; private set; }

        public void SetPackageDownloadsByVersion(string packageId, StatisticsPackagesReport report)
        {
            PackageId = packageId;
            Report = report;
        }

        public void SetPackageVersionDownloadsByClient(string packageId, string packageVersion, StatisticsPackagesReport report)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            Report = report;
        }

        public string DisplayDownloads(int downloads)
        {
            return downloads.ToString("n0", ClientCulture);
        }
    }
}
