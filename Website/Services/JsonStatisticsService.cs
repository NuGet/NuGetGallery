using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public class JsonStatisticsService : IStatisticsService
    {
        private enum Reports
        {
            RecentPopularity,           //  most frequently downloaded package registration in last 6 weeks
            RecentPopularityDetail,     //  most frequently downloaded package, specific to actual version
            RecentPopularityDetail_     //  breakout by version for a package (drill down from RecentPopularity) 
        };

        private IReportService _reportService;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesAll;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsAll;

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

        public async Task<bool> LoadDownloadPackages()
        {
            try
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
            catch (JsonReaderException e)
            {
                QuietLog.LogHandledException(e);
                return false;
            }
            catch (StorageException e)
            {
                QuietLog.LogHandledException(e);
                return false;
            }
            catch (ArgumentException e)
            {
                QuietLog.LogHandledException(e);
                return false;
            }
        }

        public async Task<bool> LoadDownloadPackageVersions()
        {
            try
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
            catch (JsonReaderException e)
            {
                QuietLog.LogHandledException(e);
                return false;
            }
            catch (StorageException e)
            {
                QuietLog.LogHandledException(e);
                return false;
            }
            catch (ArgumentException e)
            {
                QuietLog.LogHandledException(e);
                return false;
            }
        }

        public async Task<StatisticsPackagesReport> GetPackageDownloadsByVersion(string packageId)
        {
            try
            {
                if (string.IsNullOrEmpty(packageId))
                {
                    return null;
                }

                string reportName = string.Format(CultureInfo.CurrentCulture, "{0}{1}.json", Reports.RecentPopularityDetail_, packageId);

                reportName = reportName.ToLowerInvariant();

                string json = await _reportService.Load(reportName);

                if (json == null)
                {
                    return null;
                }

                JObject content = JObject.Parse(json);

                StatisticsPackagesReport report = new StatisticsPackagesReport();

                //  the report blob was there but it might be empty

                JToken downloads;
                if (content.TryGetValue("Downloads", out downloads))
                {
                    report.Total = (int)downloads;

                    JArray items = (JArray)content["Items"];

                    foreach (JObject item in items)
                    {
                        StatisticsPackagesItemViewModel row = new StatisticsPackagesItemViewModel
                        {
                            PackageVersion = (string)item["Version"],
                            Downloads = (int)item["Downloads"]
                        };

                        ((List<StatisticsPackagesItemViewModel>)report.Rows).Add(row);
                    }
                }

                return report;
            }
            catch (JsonReaderException e)
            {
                QuietLog.LogHandledException(e);
                return null;
            }
            catch (StorageException e)
            {
                QuietLog.LogHandledException(e);
                return null;
            }
            catch (ArgumentException e)
            {
                QuietLog.LogHandledException(e);
                return null;
            }
        }

        public async Task<StatisticsPackagesReport> GetPackageVersionDownloadsByClient(string packageId, string packageVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(packageVersion))
                {
                    return null;
                }

                string reportName = string.Format(CultureInfo.CurrentCulture, "{0}{1}.json", Reports.RecentPopularityDetail_, packageId);

                reportName = reportName.ToLowerInvariant();

                string json = await _reportService.Load(reportName);

                if (json == null)
                {
                    return null;
                }

                JObject content = JObject.Parse(json);

                StatisticsPackagesReport report = new StatisticsPackagesReport();

                JToken packageVersionItems;
                if (content.TryGetValue("Items", out packageVersionItems))
                {
                    // firstly find the right version - its an array and we will serach from the top (the list shouldn't be long)

                    JArray items = null;
                    foreach (JToken versionItem in (JArray)packageVersionItems)
                    {
                        if (packageVersion == (string)versionItem["Version"])
                        {
                            items = (JArray)versionItem["Items"];
                            report.Total = (int)versionItem["Downloads"];
                            break;
                        }
                    }

                    // if we couldn't find the item just return the empty report 

                    if (items == null)
                    {
                        return report;
                    }

                    // secondly create the model from the json

                    foreach (JObject item in items)
                    {
                        StatisticsPackagesItemViewModel row = new StatisticsPackagesItemViewModel
                        {
                            Client = (string)item["Client"],
                            Operation = (string)item["Operation"] == null ? "unknown" : (string)item["Operation"],
                            Downloads = (int)item["Downloads"]
                        };

                        ((List<StatisticsPackagesItemViewModel>)report.Rows).Add(row);
                    }
                }

                return report;
            }
            catch (JsonReaderException e)
            {
                QuietLog.LogHandledException(e);
                return null;
            }
            catch (StorageException e)
            {
                QuietLog.LogHandledException(e);
                return null;
            }
            catch (ArgumentException e)
            {
                QuietLog.LogHandledException(e);
                return null;
            }
        }
    }
}
