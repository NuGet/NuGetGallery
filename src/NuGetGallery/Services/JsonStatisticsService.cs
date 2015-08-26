// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        private const string RecentpopularityBlobNameFormat = "recentpopularity/{0}.json";
        private const string RecentpopularityDetailBlobNameFormat = "recentpopularity/{0}{1}.json";

        private readonly IReportService _reportService;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsSummary;
        private List<StatisticsPackagesItemViewModel> _downloadPackagesAll;
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsAll;
        private List<StatisticsNuGetUsageItem> _nuGetClientVersion;
        private List<StatisticsMonthlyUsageItem> _last6Months;

        public JsonStatisticsService(IReportService reportService)
        {
            _reportService = reportService;
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary
        {
            get
            {
                return _downloadPackagesSummary ?? (_downloadPackagesSummary = new List<StatisticsPackagesItemViewModel>());
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary
        {
            get
            {
                return _downloadPackageVersionsSummary ?? (_downloadPackageVersionsSummary = new List<StatisticsPackagesItemViewModel>());
            }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll
        {
            get { return _downloadPackagesAll ?? (_downloadPackagesAll = new List<StatisticsPackagesItemViewModel>()); }
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll
        {
            get
            {
                return _downloadPackageVersionsAll ?? (_downloadPackageVersionsAll = new List<StatisticsPackagesItemViewModel>());
            }
        }

        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion
        {
            get { return _nuGetClientVersion ?? (_nuGetClientVersion = new List<StatisticsNuGetUsageItem>()); }
        }

        public IEnumerable<StatisticsMonthlyUsageItem> Last6Months
        {
            get { return _last6Months ?? (_last6Months = new List<StatisticsMonthlyUsageItem>()); }
        }

        public async Task<StatisticsReportResult> LoadDownloadPackages()
        {
            try
            {
                var reportName = (StatisticsReportName.RecentPopularity + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var array = JArray.Parse(reportContent.Content);
                var statisticsPackagesItemViewModels = (List<StatisticsPackagesItemViewModel>)DownloadPackagesAll;
                statisticsPackagesItemViewModels.Clear();

                foreach (JObject item in array)
                {
                    statisticsPackagesItemViewModels.Add(new StatisticsPackagesItemViewModel
                    {
                        PackageId = item["PackageId"].ToString(),
                        Downloads = item["Downloads"].Value<int>()
                    });
                }

                var count = statisticsPackagesItemViewModels.Count;
                var packagesItemViewModels = (List<StatisticsPackagesItemViewModel>)DownloadPackagesSummary;
                packagesItemViewModels.Clear();

                for (int i = 0; i < Math.Min(10, count); i++)
                {
                    packagesItemViewModels.Add(statisticsPackagesItemViewModels[i]);
                }

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return StatisticsReportResult.Failed;
            }
        }

        public async Task<StatisticsReportResult> LoadDownloadPackageVersions()
        {
            try
            {
                var reportName = (StatisticsReportName.RecentPopularityDetail + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var array = JArray.Parse(reportContent.Content);
                var statisticsPackagesItemViewModels = (List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsAll;
                statisticsPackagesItemViewModels.Clear();

                foreach (JObject item in array)
                {
                    statisticsPackagesItemViewModels.Add(new StatisticsPackagesItemViewModel
                    {
                        PackageId = item["PackageId"].ToString(),
                        PackageVersion = item["PackageVersion"].ToString(),
                        Downloads = item["Downloads"].Value<int>(),
                    });
                }

                var count = statisticsPackagesItemViewModels.Count;
                var downloadPackageVersionsSummary = (List<StatisticsPackagesItemViewModel>)DownloadPackageVersionsSummary;
                downloadPackageVersionsSummary.Clear();

                for (var i = 0; i < Math.Min(10, count); i++)
                {
                    downloadPackageVersionsSummary.Add(statisticsPackagesItemViewModels[i]);
                }

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return StatisticsReportResult.Failed;
            }
        }

        public async Task<StatisticsReportResult> LoadNuGetClientVersion()
        {
            try
            {
                var reportName = (StatisticsReportName.NuGetClientVersion + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var array = JArray.Parse(reportContent.Content);
                var statisticsNuGetUsageItems = (List<StatisticsNuGetUsageItem>)NuGetClientVersion;
                statisticsNuGetUsageItems.Clear();

                foreach (JObject item in array)
                {
                    statisticsNuGetUsageItems.Add(
                        new StatisticsNuGetUsageItem
                        {
                            Version = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", item["Major"], item["Minor"]),
                            Downloads = (int)item["Downloads"]
                        });
                }

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return StatisticsReportResult.Failed;
            }
        }

        public async Task<StatisticsReportResult> LoadLast6Months()
        {
            try
            {
                var reportName = (StatisticsReportName.Last6Months + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var array = JArray.Parse(reportContent.Content);
                var statisticsMonthlyUsageItems = (List<StatisticsMonthlyUsageItem>)Last6Months;
                statisticsMonthlyUsageItems.Clear();

                foreach (JObject item in array)
                {
                    statisticsMonthlyUsageItems.Add(
                        new StatisticsMonthlyUsageItem
                        {
                            Year = (int)item["Year"],
                            MonthOfYear = (int)item["MonthOfYear"],
                            Downloads = (int)item["Downloads"]
                        });
                }

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return StatisticsReportResult.Failed;
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

                var reportName = string.Format(CultureInfo.CurrentCulture, RecentpopularityDetailBlobNameFormat,
                    StatisticsReportName.RecentPopularityDetail_, packageId).ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);

                if (reportContent == null)
                {
                    return null;
                }

                var content = JObject.Parse(reportContent.Content);
                var report = new StatisticsPackagesReport
                {
                    LastUpdatedUtc = reportContent.LastUpdatedUtc
                };

                report.Facts = CreateFacts(content);

                return report;
            }
            catch (StatisticsReportNotFoundException)
            {
                //do no logging and just return null. Since this exception will thrown for all packages which doesn't have downloads in last 6 weeks, we don't
                //want to flood the elmah logs.
                return null;
            }
            catch (NullReferenceException e)
            {
                QuietLog.LogHandledException(e);
                return null;
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

                var reportName = string.Format(CultureInfo.CurrentCulture, RecentpopularityDetailBlobNameFormat, 
                    StatisticsReportName.RecentPopularityDetail_, packageId).ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return null;
                }

                var content = JObject.Parse(reportContent.Content);
                var report = new StatisticsPackagesReport
                {
                    LastUpdatedUtc = reportContent.LastUpdatedUtc
                };

                var facts = new List<StatisticsFact>();
                foreach (var fact in CreateFacts(content))
                {
                    if (fact.Dimensions["Version"] == packageVersion)
                    {
                        facts.Add(fact);
                    }
                }

                report.Facts = facts;

                return report;
            }
            catch (NullReferenceException e)
            {
                QuietLog.LogHandledException(e);
                return null;
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

        private static IList<StatisticsFact> CreateFacts(JObject data)
        {
            IList<StatisticsFact> facts = new List<StatisticsFact>();
            JToken itemsToken;

            // Check if the "Items" exist before trying to access them.
            if (!data.TryGetValue("Items", out itemsToken))
            {
                throw new StatisticsReportNotFoundException();
            }
            foreach (JObject perVersion in data["Items"])
            {
                string version = (string)perVersion["Version"];

                foreach (JObject perClient in perVersion["Items"])
                {
                    var clientName = (string)perClient["ClientName"];
                    var clientVersion = (string)perClient["ClientVersion"];
                    var operation = "unknown";

                    JToken opt;
                    if (perClient.TryGetValue("Operation", out opt))
                    {
                        operation = (string)opt;
                    }

                    var downloads = (int)perClient["Downloads"];

                    facts.Add(new StatisticsFact(CreateDimensions(version, clientName, clientVersion, operation), downloads));
                }
            }

            return facts;
        }

        private static IDictionary<string, string> CreateDimensions(string version, string clientName, string clientVersion, string operation)
        {
            return new Dictionary<string, string>
            {
                { "Version", version },
                { "ClientName", clientName },
                { "ClientVersion", clientVersion },
                { "Operation", operation }
            };
        }
    }
}
