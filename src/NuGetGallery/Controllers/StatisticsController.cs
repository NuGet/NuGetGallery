// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;

namespace NuGetGallery
{
    public partial class StatisticsController : AppController
    {
        private readonly IStatisticsService _statisticsService = null;
        private readonly IAggregateStatsService _aggregateStatsService = null;

        public StatisticsController(IAggregateStatsService aggregateStatsService)
        {
            _statisticsService = null;
            _aggregateStatsService = aggregateStatsService;
        }

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            _aggregateStatsService = null;
        }

        public StatisticsController(IStatisticsService statisticsService, IAggregateStatsService aggregateStatsService)
        {
            _statisticsService = statisticsService;
            _aggregateStatsService = aggregateStatsService;
        }

        [HttpGet]
        [OutputCache(VaryByHeader = "Accept-Language", Duration = 120, Location = OutputCacheLocation.Server)]
        public virtual async Task<ActionResult> Totals()
        {
            var stats = await _aggregateStatsService.GetAggregateStats();

            // if we fail to detect client locale from the Languages header, fall back to server locale
            CultureInfo clientCulture = DetermineClientLocale() ?? CultureInfo.CurrentCulture;
            return Json(
                new
                {
                    Downloads = stats.Downloads.ToString("n0", clientCulture),
                    UniquePackages = stats.UniquePackages.ToString("n0", clientCulture),
                    TotalPackages = stats.TotalPackages.ToString("n0", clientCulture),
                    LastUpdatedDateUtc = stats.LastUpdateDateUtc
                },
                JsonRequestBehavior.AllowGet);
        }

        private CultureInfo DetermineClientLocale()
        {
            if (Request == null)
            {
                return null;
            }

            string[] languages = Request.UserLanguages;
            if (languages == null)
            {
                return null;
            }

            foreach (string language in languages)
            {
                string lang = language.ToLowerInvariant().Trim();
                try
                {
                    return CultureInfo.GetCultureInfo(lang);
                }
                catch (CultureNotFoundException)
                {
                }
            }

            foreach (string language in languages)
            {
                string lang = language.ToLowerInvariant().Trim();
                if (lang.Length > 2)
                {
                    string lang2 = lang.Substring(0, 2);
                    try
                    {
                        return CultureInfo.GetCultureInfo(lang2);
                    }
                    catch (CultureNotFoundException)
                    {
                    }
                }
            }

            return null;
        }

        //
        // GET: /stats

        public virtual async Task<ActionResult> Index()
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var availablity = await Task.WhenAll(
                _statisticsService.LoadDownloadPackages(), 
                _statisticsService.LoadDownloadPackageVersions(),
                _statisticsService.LoadNuGetClientVersion(),
                _statisticsService.LoadLast6Months());

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = availablity[0].Loaded,
                DownloadPackagesSummary = _statisticsService.DownloadPackagesSummary,
                IsDownloadPackageDetailAvailable = availablity[1].Loaded,
                DownloadPackageVersionsSummary = _statisticsService.DownloadPackageVersionsSummary,
                IsNuGetClientVersionAvailable = availablity[2].Loaded,
                NuGetClientVersion = _statisticsService.NuGetClientVersion,
                IsLast6MonthsAvailable = availablity[3].Loaded,
                Last6Months = _statisticsService.Last6Months,
                LastUpdatedUtc = availablity
                    .Where(r => r.LastUpdatedUtc.HasValue)
                    .OrderByDescending(r => r.LastUpdatedUtc.Value)
                    .Select(r => r.LastUpdatedUtc)
                    .FirstOrDefault()
            };

            model.ClientCulture = DetermineClientLocale();

            model.Update();

            model.UseD3 = UseD3();

            return View(model);
        }

        //
        // GET: /stats/packages

        public virtual async Task<ActionResult> Packages()
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var result = await _statisticsService.LoadDownloadPackages();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = result.Loaded,
                DownloadPackagesAll = _statisticsService.DownloadPackagesAll,
                ClientCulture = DetermineClientLocale(),
                LastUpdatedUtc = result.LastUpdatedUtc
            };

            return View(model);
        }

        //
        // GET: /stats/packageversions

        public virtual async Task<ActionResult> PackageVersions()
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var result = await _statisticsService.LoadDownloadPackageVersions();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageDetailAvailable = result.Loaded,
                DownloadPackageVersionsAll = _statisticsService.DownloadPackageVersionsAll,
                ClientCulture = DetermineClientLocale(),
                LastUpdatedUtc = result.LastUpdatedUtc
            };

            return View(model);
        }

        //
        // GET: /stats/package/{id}

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id, string[] groupby)
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            StatisticsPackagesReport report = await _statisticsService.GetPackageDownloadsByVersion(id);

            ProcessReport(report, groupby, new string[] { "Version", "ClientName", "ClientVersion", "Operation" }, id, DetermineClientLocale());

            if (report != null)
            {
                report.Id = MakeReportId(groupby);
            }

            StatisticsPackagesViewModel model = new StatisticsPackagesViewModel();

            model.SetPackageDownloadsByVersion(id, report);

            model.UseD3 = UseD3();

            return View(model);
        }

        //
        // GET: /stats/package/{id}/{version}

        public virtual async Task<ActionResult> PackageDownloadsDetail(string id, string version, string[] groupby)
        {
            if (_statisticsService == NullStatisticsService.Instance)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            StatisticsPackagesReport report = await _statisticsService.GetPackageVersionDownloadsByClient(id, version);

            ProcessReport(report, groupby, new string[] { "ClientName", "ClientVersion", "Operation" }, null, DetermineClientLocale());

            if (report != null)
            {
                report.Id = MakeReportId(groupby);
            }

            var model = new StatisticsPackagesViewModel();

            model.SetPackageVersionDownloadsByClient(id, version, report);

            model.UseD3 = UseD3();

            return View(model);
        }

        private void ProcessReport(StatisticsPackagesReport report, string[] groupby, string[] dimensions, string id, CultureInfo clientCulture)
        {
            if (report == null)
            {
                return;
            }

            string[] pivot = new string[4];

            if (groupby != null)
            {
                //  process and validate the groupby query. unrecognized fields are ignored. others fields regarded for existance

                int dim = 0;

                foreach (string dimension in dimensions)
                {
                    CheckGroupBy(groupby, dimension, pivot, ref dim, report);
                }

                if (dim == 0)
                {
                    // no recognized fields so just fall into the null logic

                    groupby = null;
                }
                else
                {
                    // the pivot array is used as the Columns in the report so we resize because this was the final set of columns 

                    Array.Resize(ref pivot, dim);
                }

                Tuple<StatisticsPivot.TableEntry[][], string> result = StatisticsPivot.GroupBy(report.Facts, pivot, clientCulture);

                if (id != null)
                {
                    int col = Array.FindIndex(pivot, (s) => s.Equals("Version", StringComparison.Ordinal));
                    if (col >= 0)
                    {
                        for (int row = 0; row < result.Item1.GetLength(0); row++)
                        {
                            StatisticsPivot.TableEntry entry = result.Item1[row][col];
                            if (entry != null)
                            {
                                entry.Uri = Url.Package(id, entry.Data);
                            }
                        }
                    }
                }

                report.Table = result.Item1;
                report.Total = result.Item2;
                report.Columns = pivot.Select(GetDimensionDisplayName);
            }

            if (groupby == null)
            {
                //  degenerate case (but still logically valid)

                foreach (string dimension in dimensions)
                {
                    report.Dimensions.Add(new StatisticsDimension { Value = dimension, DisplayName = GetDimensionDisplayName(dimension), IsChecked = false });
                }

                report.Table = null;
                report.Total = report.Facts.Sum(fact => fact.Amount).ToString("n0", clientCulture);
            }
        }

        private static void CheckGroupBy(string[] groupby, string name, string[] pivot, ref int dimension, StatisticsPackagesReport report)
        {
            if (Array.Exists(groupby, (s) => s.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                pivot[dimension++] = name;
                report.Dimensions.Add(new StatisticsDimension { Value = name, DisplayName = GetDimensionDisplayName(name), IsChecked = true });
            }
            else
            {
                report.Dimensions.Add(new StatisticsDimension { Value = name, DisplayName = GetDimensionDisplayName(name), IsChecked = false });
            }
        }

        private static string GetDimensionDisplayName(string name)
        {
            switch (name)
            {
                case "ClientName":
                    return "Client Name";
                case "ClientVersion":
                    return "Client Version";
                default:
                    return name;
            }
        }

        private static string MakeReportId(string[] groupby)
        {
            string graphId = "report-";
            if (groupby != null)
            {
                foreach (string g in groupby)
                {
                    graphId += g;
                }
            }
            return graphId;
        }

        private bool UseD3()
        {
            //  the aim here is to explicit eliminate IE 7.0 and IE 8.0 from the browsers that support D3
            //  we are doing this on the server rather than in the browser because even downloading the D3 script fails
            bool f = true;
            if (Request != null && Request.Browser != null && Request.Browser.Browser == "IE")
            {
                float version;
                if (float.TryParse(Request.Browser.Version, out version))
                {
                    f = version > 8.0;
                }
            }
            return f;
        }
    }
}
