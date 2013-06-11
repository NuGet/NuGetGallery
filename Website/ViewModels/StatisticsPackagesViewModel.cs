using System.Collections.Generic;

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

        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion
        {
            get; set;
        }

        public IEnumerable<StatisticsMonthlyUsageItem> Last6Months
        {
            get; set;
        }

        public StatisticsPackagesReport Report
        {
            get;
            private set;
        }

        public bool IsDownloadPackageAvailable { get; set; }
        public bool IsDownloadPackageDetailAvailable { get; set; }
        public bool IsNuGetClientVersionAvailable { get; set; }
        public bool IsLast6MonthsAvailable { get; set; }

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

        private static string[] _months = { string.Empty, "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        public string DisplayMonth(int year, int monthOfYear)
        {
            if (monthOfYear < 1 || monthOfYear > 12)
            {
                return string.Empty;
            }
            return string.Format("{0} {1}", year, _months[monthOfYear]);
        }
    }
}
