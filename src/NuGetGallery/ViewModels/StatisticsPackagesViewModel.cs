using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        private DateTime? _lastUpdatedUtc;

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
        
        public CultureInfo ClientCulture { get; set; }

        public StatisticsPackagesReport Report
        {
            get;
            private set;
        }

        public bool IsDownloadPackageAvailable { get; set; }
        public bool IsDownloadPackageDetailAvailable { get; set; }
        public bool IsNuGetClientVersionAvailable { get; set; }
        public bool IsLast6MonthsAvailable { get; set; }

        public int NuGetClientVersionTotalDownloads { get; private set; }

        public bool IsReportAvailable { get { return (Report != null); } }

        public string PackageId { get; private set; }
        public string PackageVersion { get; private set; }

        public bool UseD3
        {
            get;
            set;
        }

        public DateTime? LastUpdatedUtc
        {
            get { return Report == null ? _lastUpdatedUtc : Report.LastUpdatedUtc; }
            set { _lastUpdatedUtc = value; }
        }

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

        public void Update()
        {
            if (IsNuGetClientVersionAvailable)
            {
                NuGetClientVersionTotalDownloads = NuGetClientVersion.Sum(item => item.Downloads);    
            }
        }

        private static string[] _months = { string.Empty, "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        public string DisplayMonth(int year, int monthOfYear)
        {
            if (monthOfYear < 1 || monthOfYear > 12)
            {
                return string.Empty;
            }
            return string.Format(ClientCulture, "{0} {1}", year, _months[monthOfYear]);
        }

        public string DisplayPercentage(float amount, float total)
        {
            return (amount / total).ToString("P0", ClientCulture);
        }
    }
}
