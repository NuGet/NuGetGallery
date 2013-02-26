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

        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsByVersion
        {
            get;
            private set;
        }

        public bool IsDownloadPackageAvailable { get; set; }
        public bool IsDownloadPackageDetailAvailable { get; set; }
        public bool IsDownloadPackageByVersionAvailable { get; set; }

        public string PackageId { get; private set; }
        public int TotalPackageDownloads { get; private set; }

        public void SetPackageDownloadsByVersion(string id, bool isAvailable, IEnumerable<StatisticsPackagesItemViewModel> packageDownloadsByVersion)
        {
            PackageId = id;
            IsDownloadPackageByVersionAvailable = isAvailable;
            PackageDownloadsByVersion = packageDownloadsByVersion;

            // if the report was not available the following code will be a no-op. But there should never be any null exceptions.

            TotalPackageDownloads = 0;
            foreach (StatisticsPackagesItemViewModel item in PackageDownloadsByVersion)
            {
                TotalPackageDownloads += item.Downloads;
            }
        }
    }
}
