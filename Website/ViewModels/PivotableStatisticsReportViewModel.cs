using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Statistics;

namespace NuGetGallery.ViewModels
{
    public class PivotableStatisticsReportViewModel
    {
        public string PackageId { get; private set; }
        public string PackageVersion { get; private set; }
        public DownloadStatisticsReport Report { get; private set; }

        public PivotableStatisticsReportViewModel(string packageId, DownloadStatisticsReport report)
        {
            PackageId = packageId;
            Report = report;
        }

        public PivotableStatisticsReportViewModel(string packageId, string packageVersion, DownloadStatisticsReport report)
            : this(packageId, report)
        {
            PackageVersion = packageVersion;
        }
    }
}