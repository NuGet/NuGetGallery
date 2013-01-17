using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class JsonStatisticsService : IStatisticsService
    {
        private enum Reports
        {
            RecentPopularity,           //  most frequently downloaded package registration in last 6 weeks
            RecentPopularityDetail,     //  most frequently downloaded package, specific to actual version
            RecentPopularity_           //  breakout by version for a package (drill down from RecentPopularity) 
        };

        private IReportService _reportService;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesAll;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsAll;
        private List<StatisticsPackagesItemViewModel> _packageDownloadsByVersion;

        public JsonStatisticsService(IReportService reportService)
        {
            _reportService = reportService;
        }

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

        public async Task<bool> LoadDownloadPackages()
        {
            string json = await _reportService.Load(Reports.RecentPopularity.ToString() + ".json");

            if (json == null)
            {
                return false;
            }

            JArray array = JArray.Parse(json);

            ((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll).Clear();

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll).Add(new StatisticsPackagesItemViewModel
                {
                    PackageId = item["PackageId"].ToString(),
                    Downloads = item["Downloads"].Value<int>()
                });
            }

            int count = ((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll).Count;

            ((List<StatisticsPackagesItemViewModel>)DownloadPackagesSummary).Clear();

            for (int i = 0; i < Math.Min(10, count); i++)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackagesSummary).Add(((List<StatisticsPackagesItemViewModel>)DownloadPackagesAll)[i]);
            }

            return true;
        }

        public async Task<bool> LoadDownloadPackageVersions()
        {
            string json = await _reportService.Load(Reports.RecentPopularityDetail.ToString() + ".json");

            if (json == null)
            {
                return false;
            }

            JArray array = JArray.Parse(json);

            ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll).Clear();

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll).Add(new StatisticsPackagesItemViewModel
                {
                    PackageId = item["PackageId"].ToString(),
                    PackageVersion = item["PackageVersion"].ToString(),
                    Downloads = item["Downloads"].Value<int>()
                });
            }

            int count = ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll).Count;

            ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsSummary).Clear();

            for (int i = 0; i < Math.Min(10, count); i++)
            {
                ((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsSummary).Add(((List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll)[i]);
            }

            return true;
        }

        public async Task LoadPackageDownloadsByVersion(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            string reportName = string.Format(CultureInfo.CurrentCulture, "{0}{1}.json", Reports.RecentPopularity_, id);

            string json = await _reportService.Load(reportName);

            if (json == null)
            {
                return;
            }

            JArray array = JArray.Parse(json);

            ((List<StatisticsPackagesItemViewModel>)PackageDownloadsByVersion).Clear();

            foreach (JObject item in array)
            {
                ((List<StatisticsPackagesItemViewModel>)PackageDownloadsByVersion).Add(new StatisticsPackagesItemViewModel
                {
                    PackageVersion = item["PackageVersion"].ToString(),
                    Downloads = item["Downloads"].Value<int>()
                });
            }
        }
    }
}
