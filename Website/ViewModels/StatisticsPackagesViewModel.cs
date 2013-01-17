using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        IStatisticsService _statisticsService;

        public StatisticsPackagesViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary 
        {
            get
            {
                return _statisticsService.DownloadPackagesSummary;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        {
            get
            {
                return _statisticsService.DownloadPackageVersionsSummary;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        {
            get
            {
                return _statisticsService.DownloadPackagesAll;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        {
            get
            {
                return _statisticsService.DownloadPackageVersionsAll;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsByVersion
        {
            get
            {
                return _statisticsService.PackageDownloadsByVersion;
            }
        }

        public bool IsDownloadPackageAvailable { get; private set; }
        public bool IsDownloadPackageDetailAvailable { get; private set; }
        public string PackageId { get; private set; }
        public int TotalPackageDownloads { get; private set; }

        public async Task LoadDownloadPackages()
        {
            IsDownloadPackageAvailable = await _statisticsService.LoadDownloadPackages();
        }

        public async Task LoadDownloadPackageVersions()
        {
            IsDownloadPackageDetailAvailable = await _statisticsService.LoadDownloadPackageVersions();
        }

        public async Task LoadPackageDownloadsByVersion(string id)
        {
            await _statisticsService.LoadPackageDownloadsByVersion(id);
            PackageId = id;
            TotalPackageDownloads = 0;

            foreach (StatisticsPackagesItemViewModel item in PackageDownloadsByVersion)
            {
                TotalPackageDownloads += item.Downloads;
            }
        }
    }
}
