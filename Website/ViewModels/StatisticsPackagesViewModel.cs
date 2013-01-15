using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        private IStatisticsService _statisticsService;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesAll;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsAll;
        private List<StatisticsPackagesItemViewModel> _packageDownloadsByVersion;

        public StatisticsPackagesViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        public bool IsDownloadPackageAvailable { get; private set; }
        public bool IsDownloadPackageDetailAvailable { get; private set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary 
        {
            get
            {
                if (_downloadPackagesSummary == null)
                {
                    _downloadPackagesSummary = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackagesSummary;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        {
            get
            {
                if (_downloadPackageVersionsSummary == null)
                {
                    _downloadPackageVersionsSummary = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackageVersionsSummary;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        {
            get
            {
                if (_downloadPackagesAll == null)
                {
                    _downloadPackagesAll = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackagesAll;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        {
            get
            {
                if (_downloadPackageVersionsAll == null)
                {
                    _downloadPackageVersionsAll = new List<StatisticsPackagesItemViewModel>();
                }
                return _downloadPackageVersionsAll;
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsByVersion
        {
            get
            {
                if (_packageDownloadsByVersion == null)
                {
                    _packageDownloadsByVersion = new List<StatisticsPackagesItemViewModel>();
                }
                return _packageDownloadsByVersion;
            }
        }

        public string PackageId { get; private set; }
        public int TotalPackageDownloads { get; private set; }

        public void LoadDownloadPackages()
        {
            JArray array = _statisticsService.LoadReport("RecentPopularity.json");

            if (array == null)
            {
                IsDownloadPackageAvailable = false;
                return;
            }

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll).Add(new StatisticsPackagesItemViewModel
                {
                    PackageId = item["PackageId"].ToString(),
                    Downloads = int.Parse(item["Downloads"].ToString())
                });
            }

            for (int i = 0; i < 10; i++)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackagesSummary).Add(((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll)[i]);
            }

            IsDownloadPackageAvailable = true;
        }

        public void LoadDownloadPackageVersions()
        {
            JArray array = _statisticsService.LoadReport("RecentPopularityDetail.json");

            if (array == null)
            {
                IsDownloadPackageDetailAvailable = false;
                return;
            }

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll).Add(new StatisticsPackagesItemViewModel
                {
                    PackageId = item["PackageId"].ToString(),
                    PackageVersion = item["PackageVersion"].ToString(),
                    Downloads = int.Parse(item["Downloads"].ToString())
                });
            }

            for (int i = 0; i < 10; i++)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsSummary).Add(((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll)[i]);
            }

            IsDownloadPackageDetailAvailable = true;
        }

        public void LoadPackageDownloadsByVersion(string id)
        {
            if (id == string.Empty)
            {
                return;
            }

            JArray array = _statisticsService.LoadReport(string.Format("RecentPopularity_{0}.json", id));

            if (array == null)
            {
                return;
            }

            this.TotalPackageDownloads = 0;

            foreach (JObject item in array)
            {
                int downloads = int.Parse(item["Downloads"].ToString());

                ((List<StatisticsPackagesItemViewModel>)PackageDownloadsByVersion).Add(new StatisticsPackagesItemViewModel
                {
                    PackageVersion = item["PackageVersion"].ToString(),
                    Downloads = downloads
                });

                this.TotalPackageDownloads += downloads;
            }

            this.PackageId = id;
        }
    }
}
