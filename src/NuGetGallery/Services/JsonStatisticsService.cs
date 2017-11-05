// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public class JsonStatisticsService : IStatisticsService
    {
        public readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);
        private const string _recentpopularityDetailBlobNameFormat = "recentpopularity/{0}{1}.json";

        private DateTime? _lastRefresh = null;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly IReportService _reportService;

        private List<StatisticsPackagesItemViewModel> _downloadPackagesAll = new List<StatisticsPackagesItemViewModel>();
        private List<StatisticsPackagesItemViewModel> _downloadPackagesSummary = new List<StatisticsPackagesItemViewModel>();

        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsAll = new List<StatisticsPackagesItemViewModel>();
        private List<StatisticsPackagesItemViewModel> _downloadPackageVersionsSummary = new List<StatisticsPackagesItemViewModel>();

        private List<StatisticsPackagesItemViewModel> _downloadCommunityPackagesAll = new List<StatisticsPackagesItemViewModel>();
        private List<StatisticsPackagesItemViewModel> _downloadCommunityPackagesSummary = new List<StatisticsPackagesItemViewModel>();

        private List<StatisticsPackagesItemViewModel> _downloadCommunityPackageVersionsAll = new List<StatisticsPackagesItemViewModel>();
        private List<StatisticsPackagesItemViewModel> _downloadCommunityPackageVersionsSummary = new List<StatisticsPackagesItemViewModel>();

        private List<StatisticsNuGetUsageItem> _nuGetClientVersion = new List<StatisticsNuGetUsageItem>();
        private List<StatisticsWeeklyUsageItem> _last6Weeks = new List<StatisticsWeeklyUsageItem>();

        public JsonStatisticsService(IReportService reportService)
        {
            _reportService = reportService;
        }

        public StatisticsReportResult DownloadPackagesResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll => _downloadPackagesAll;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary => _downloadPackagesSummary;

        public StatisticsReportResult DownloadPackageVersionsResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll => _downloadPackageVersionsAll;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary => _downloadPackageVersionsSummary;

        public StatisticsReportResult DownloadCommunityPackagesResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesAll => _downloadCommunityPackagesAll;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesSummary => _downloadCommunityPackagesSummary;

        public StatisticsReportResult DownloadCommunityPackageVersionsResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsAll => _downloadCommunityPackageVersionsAll;
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsSummary => _downloadCommunityPackageVersionsSummary;

        public StatisticsReportResult NuGetClientVersionResult { get; private set; }
        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion => _nuGetClientVersion;

        public StatisticsReportResult Last6WeeksResult { get; private set; }
        public IEnumerable<StatisticsWeeklyUsageItem> Last6Weeks => _last6Weeks;

        public DateTime? LastUpdatedUtc { get; private set; } = null;

        public async Task Refresh()
        {
            if (!ShouldRefresh())
            {
                return;
            }

            await _semaphoreSlim.WaitAsync();

            try
            {
                if (!ShouldRefresh())
                {
                    return;
                }

                var availablity = await Task.WhenAll(
                    LoadDownloadPackages(),
                    LoadDownloadPackageVersions(),
                    LoadDownloadCommunityPackages(),
                    LoadDownloadCommunityPackageVersions(),
                    LoadNuGetClientVersion(),
                    LoadLast6Weeks());

                DownloadPackagesResult = availablity[0];
                DownloadPackageVersionsResult = availablity[1];
                DownloadCommunityPackagesResult = availablity[2];
                DownloadCommunityPackageVersionsResult = availablity[3];
                NuGetClientVersionResult = availablity[4];
                Last6WeeksResult = availablity[5];

                LastUpdatedUtc = availablity
                    .Where(r => r.LastUpdatedUtc.HasValue)
                    .OrderByDescending(r => r.LastUpdatedUtc.Value)
                    .Select(r => r.LastUpdatedUtc)
                    .FirstOrDefault();

                _lastRefresh = DateTime.UtcNow;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private bool ShouldRefresh() => (!_lastRefresh.HasValue || (_lastRefresh - DateTime.UtcNow) >= RefreshInterval);

        private Task<StatisticsReportResult> LoadDownloadPackages()
        {
            return LoadDownloadPackages(
                StatisticsReportName.RecentPopularity,
                _downloadPackagesAll,
                _downloadPackagesSummary);
        }

        private Task<StatisticsReportResult> LoadDownloadCommunityPackages()
        {
            return LoadDownloadPackages(
                StatisticsReportName.RecentCommunityPopularity,
                _downloadCommunityPackagesAll,
                _downloadCommunityPackagesSummary);
        }

        /// <summary>
        /// Load a package downloads report.
        /// </summary>
        /// <param name="statisticsReportName">The name of the report to load.</param>
        /// <param name="packagesAll">The model that should be hydrated with the report.</param>
        /// <param name="packagesSummary">The model that should be hydrated with the summary of the report.</param>
        /// <returns>The result of loading the report.</returns>
        private async Task<StatisticsReportResult> LoadDownloadPackages(
            StatisticsReportName statisticsReportName,
            List<StatisticsPackagesItemViewModel> packagesAll,
            List<StatisticsPackagesItemViewModel> packagesSummary)
        {
            try
            {
                var reportName = (statisticsReportName + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var results = JArray.Parse(reportContent.Content).Select(item =>
                    new StatisticsPackagesItemViewModel
                    {
                        PackageId = item["PackageId"].ToString(),
                        Downloads = item["Downloads"].Value<int>()
                    }
                );

                packagesAll.Clear();
                packagesAll.AddRange(results);

                packagesSummary.Clear();
                packagesSummary.AddRange(packagesAll.Take(10));

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return StatisticsReportResult.Failed;
            }
        }

        private Task<StatisticsReportResult> LoadDownloadPackageVersions()
        {
            return LoadDownloadPackageVersions(
                StatisticsReportName.RecentPopularityDetail,
                _downloadPackageVersionsAll,
                _downloadPackageVersionsSummary);
        }

        private Task<StatisticsReportResult> LoadDownloadCommunityPackageVersions()
        {
            return LoadDownloadPackageVersions(
                StatisticsReportName.RecentCommunityPopularityDetail,
                _downloadCommunityPackageVersionsAll,
                _downloadCommunityPackageVersionsSummary);
        }

        /// <summary>
        /// Load a package version downloads report.
        /// </summary>
        /// <param name="statisticsReportName">The name of the report to load.</param>
        /// <param name="packageVersionsAll">The model that should be hydrated with the report.</param>
        /// <returns>The result of loading the report.</returns>
        private async Task<StatisticsReportResult> LoadDownloadPackageVersions(
            StatisticsReportName statisticsReportName,
            List<StatisticsPackagesItemViewModel> packageVersionsAll,
            List<StatisticsPackagesItemViewModel> packageVersionsSummary)
        {
            try
            {
                var reportName = (statisticsReportName + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var results = JArray.Parse(reportContent.Content).Select(item =>
                    new StatisticsPackagesItemViewModel
                    {
                        PackageId = item["PackageId"].ToString(),
                        PackageVersion = item["PackageVersion"].ToString(),
                        Downloads = item["Downloads"].Value<int>(),
                    }
                );

                packageVersionsAll.Clear();
                packageVersionsAll.AddRange(results);

                packageVersionsSummary.Clear();
                packageVersionsSummary.AddRange(packageVersionsAll.Take(10));

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                return StatisticsReportResult.Failed;
            }
        }

        private async Task<StatisticsReportResult> LoadNuGetClientVersion()
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

        private async Task<StatisticsReportResult> LoadLast6Weeks()
        {
            try
            {
                var reportName = (StatisticsReportName.Last6Weeks + ".json").ToLowerInvariant();
                var reportContent = await _reportService.Load(reportName);
                if (reportContent == null)
                {
                    return StatisticsReportResult.Failed;
                }

                var array = JArray.Parse(reportContent.Content);
                var statisticsMonthlyUsageItems = (List<StatisticsWeeklyUsageItem>)Last6Weeks;
                statisticsMonthlyUsageItems.Clear();

                foreach (JObject item in array)
                {
                    statisticsMonthlyUsageItems.Add(
                        new StatisticsWeeklyUsageItem
                        {
                            Year = (int)item["Year"],
                            WeekOfYear = (int)item["WeekOfYear"],
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

                var reportName = string.Format(CultureInfo.CurrentCulture, _recentpopularityDetailBlobNameFormat,
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

                var reportName = string.Format(CultureInfo.CurrentCulture, _recentpopularityDetailBlobNameFormat,
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
