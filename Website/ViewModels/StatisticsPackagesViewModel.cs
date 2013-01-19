using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

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
        public string PackageId { get; private set; }
        public int TotalPackageDownloads { get; private set; }

        //public async Task LoadDownloadPackages()
        //{
        //    IsDownloadPackageAvailable = await _statisticsService.LoadDownloadPackages();
        //}

        //public async Task LoadDownloadPackageVersions()
        //{
        //    IsDownloadPackageDetailAvailable = await _statisticsService.LoadDownloadPackageVersions();
        //}

        //public async Task LoadPackageDownloadsByVersion(string id)
        //{
        //    await _statisticsService.LoadPackageDownloadsByVersion(id);
        //    PackageId = id;
        //    TotalPackageDownloads = 0;

        //    foreach (StatisticsPackagesItemViewModel item in PackageDownloadsByVersion)
        //    {
        //        TotalPackageDownloads += item.Downloads;
        //    }
        //}

        public void SetPackageDownloadsByVersion(string id, IEnumerable<StatisticsPackagesItemViewModel> packageDownloadsByVersion)
        {
            PackageId = id;
            PackageDownloadsByVersion = packageDownloadsByVersion;

            TotalPackageDownloads = 0;
            foreach (StatisticsPackagesItemViewModel item in PackageDownloadsByVersion)
            {
                TotalPackageDownloads += item.Downloads;
            }
        }
    }
}
