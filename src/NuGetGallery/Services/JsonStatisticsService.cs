// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public class JsonStatisticsService : IStatisticsService
    {
        private const string RecentPopularityDetailBlobNameFormat = "recentpopularity/{0}{1}.json";

        /// <summary>
        /// How often statistics reports should be refreshed using the <see cref="_reportService"/>.
        /// </summary>
        private readonly TimeSpan _refreshInterval = TimeSpan.FromHours(1);

        /// <summary>
        /// The last time the reports were loaded, or null.
        /// </summary>
        private DateTime? _lastRefresh = null;

        /// <summary>
        /// The service used to load reports in the form of JSON blobs.
        /// </summary>
        private readonly IReportService _reportService;

        /// <summary>
        /// Mockable source of current time.
        /// </summary>
        private readonly IDateTimeProvider _dateTimeProvider;

        /// <summary>
        /// The semaphore used to update the statistics service's reports.
        /// </summary>
        private readonly SemaphoreSlim _reportSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private readonly List<StatisticsPackagesItemViewModel> _packageDownloads = new List<StatisticsPackagesItemViewModel>();
        private readonly List<StatisticsPackagesItemViewModel> _packageDownloadsSummary = new List<StatisticsPackagesItemViewModel>();

        private readonly List<StatisticsPackagesItemViewModel> _packageVersionDownloads = new List<StatisticsPackagesItemViewModel>();
        private readonly List<StatisticsPackagesItemViewModel> _packageVersionDownloadsSummary = new List<StatisticsPackagesItemViewModel>();

        private readonly List<StatisticsPackagesItemViewModel> _communityPackageDownloads = new List<StatisticsPackagesItemViewModel>();
        private readonly List<StatisticsPackagesItemViewModel> _communityPackageDownloadsSummary = new List<StatisticsPackagesItemViewModel>();

        private readonly List<StatisticsPackagesItemViewModel> _communityPackageVersionDownloads = new List<StatisticsPackagesItemViewModel>();
        private readonly List<StatisticsPackagesItemViewModel> _communityPackageVersionDownloadsSummary = new List<StatisticsPackagesItemViewModel>();

        private readonly List<StatisticsNuGetUsageItem> _nuGetClientVersion = new List<StatisticsNuGetUsageItem>();
        private readonly List<StatisticsWeeklyUsageItem> _last6Weeks = new List<StatisticsWeeklyUsageItem>();

        public JsonStatisticsService(IReportService reportService, IDateTimeProvider dateTimeProvider)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        }

        public StatisticsReportResult PackageDownloadsResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloads => _packageDownloads;
        public IEnumerable<StatisticsPackagesItemViewModel> PackageDownloadsSummary => _packageDownloadsSummary;

        public StatisticsReportResult PackageVersionDownloadsResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> PackageVersionDownloads => _packageVersionDownloads;
        public IEnumerable<StatisticsPackagesItemViewModel> PackageVersionDownloadsSummary => _packageVersionDownloadsSummary;

        public StatisticsReportResult CommunityPackageDownloadsResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageDownloads => _communityPackageDownloads;
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageDownloadsSummary => _communityPackageDownloadsSummary;

        public StatisticsReportResult CommunityPackageVersionDownloadsResult { get; private set; }
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageVersionDownloads => _communityPackageVersionDownloads;
        public IEnumerable<StatisticsPackagesItemViewModel> CommunityPackageVersionDownloadsSummary => _communityPackageVersionDownloadsSummary;

        public StatisticsReportResult NuGetClientVersionResult { get; private set; }
        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion => _nuGetClientVersion;

        public StatisticsReportResult Last6WeeksResult { get; private set; }
        public IEnumerable<StatisticsWeeklyUsageItem> Last6Weeks => _last6Weeks;

        /// <summary>
        /// The time that the reports were generated, or null if the reports have not been loaded.
        /// </summary>
        public DateTime? LastUpdatedUtc { get; private set; } = null;

        /// <summary>
        /// Refresh or load the statistics service's reports. No-ops if <see cref="_lastRefresh"/>
        /// is within <see cref="_refreshInterval"/>.
        /// </summary>
        /// <returns>A task that completes when the reports have finished.</returns>
        public async Task Refresh()
        {
            if (!ShouldRefresh())
            {
                return;
            }

            await _reportSemaphore.WaitAsync();

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

                PackageDownloadsResult = availablity[0];
                PackageVersionDownloadsResult = availablity[1];
                CommunityPackageDownloadsResult = availablity[2];
                CommunityPackageVersionDownloadsResult = availablity[3];
                NuGetClientVersionResult = availablity[4];
                Last6WeeksResult = availablity[5];

                LastUpdatedUtc = availablity
                    .Where(r => r.LastUpdatedUtc.HasValue)
                    .OrderByDescending(r => r.LastUpdatedUtc.Value)
                    .Select(r => r.LastUpdatedUtc)
                    .FirstOrDefault();

                _lastRefresh = _dateTimeProvider.UtcNow;
            }
            finally
            {
                _reportSemaphore.Release();
            }
        }

        private bool ShouldRefresh()
        {
            // The reports should be refreshed if they have never been loaded, or, if
            // the reports are stale and have reached the refresh interval.
            if (!_lastRefresh.HasValue)
            {
                return true;
            }

            return (_dateTimeProvider.UtcNow - _lastRefresh) >= _refreshInterval;
        }

        private Task<StatisticsReportResult> LoadDownloadPackages()
        {
            return LoadDownloadPackages(
                StatisticsReportName.RecentPopularity,
                _packageDownloads,
                _packageDownloadsSummary);
        }

        private Task<StatisticsReportResult> LoadDownloadCommunityPackages()
        {
            return LoadDownloadPackages(
                StatisticsReportName.RecentCommunityPopularity,
                _communityPackageDownloads,
                _communityPackageDownloadsSummary);
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
                        Downloads = item["Downloads"].Value<long>()
                    }
                );

                packagesAll.Clear();
                packagesAll.AddRange(results);

                packagesSummary.Clear();
                packagesSummary.AddRange(packagesAll.Take(10));

                return StatisticsReportResult.Success(reportContent.LastUpdatedUtc);
            }
            catch (Exception)
            {
                return StatisticsReportResult.Failed;
            }
        }

        private Task<StatisticsReportResult> LoadDownloadPackageVersions()
        {
            return LoadDownloadPackageVersions(
                StatisticsReportName.RecentPopularityDetail,
                _packageVersionDownloads,
                _packageVersionDownloadsSummary);
        }

        private Task<StatisticsReportResult> LoadDownloadCommunityPackageVersions()
        {
            return LoadDownloadPackageVersions(
                StatisticsReportName.RecentCommunityPopularityDetail,
                _communityPackageVersionDownloads,
                _communityPackageVersionDownloadsSummary);
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
                        Downloads = item["Downloads"].Value<long>(),
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
                            Downloads = (long)item["Downloads"]
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
                            Downloads = (long)item["Downloads"]
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

                var reportName = string.Format(CultureInfo.CurrentCulture, RecentPopularityDetailBlobNameFormat,
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
                //want to flood the logs.
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
            catch (CloudBlobStorageException e)
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

                var reportName = string.Format(CultureInfo.CurrentCulture, RecentPopularityDetailBlobNameFormat,
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
            catch (CloudBlobStorageException e)
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

            // Check if the "Items" exist before trying to access them.
            if (!data.TryGetValue("Items", out _))
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

                    var downloads = (long)perClient["Downloads"];

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
