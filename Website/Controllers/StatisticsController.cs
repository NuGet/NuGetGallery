using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;

namespace NuGetGallery
{
    public partial class StatisticsController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IAggregateStatsService _aggregateStatsService;

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
        public virtual JsonResult Totals()
        {
            var stats = _aggregateStatsService.GetAggregateStats();

            // if we fail to detect client locale from the Languages header, fall back to server locale
            CultureInfo clientCulture = DetermineClientLocale() ?? CultureInfo.CurrentCulture;
            return Json(
                new
                {
                    Downloads = stats.Downloads.ToString("n0", clientCulture),
                    UniquePackages = stats.UniquePackages.ToString("n0", clientCulture),
                    TotalPackages = stats.TotalPackages.ToString("n0", clientCulture)
                },
                JsonRequestBehavior.AllowGet);
        }

        private CultureInfo DetermineClientLocale()
        {
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
            if (_statisticsService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            bool[] availablity = await Task.WhenAll(_statisticsService.LoadDownloadPackages(), _statisticsService.LoadDownloadPackageVersions());

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = availablity[0],
                DownloadPackagesSummary = _statisticsService.DownloadPackagesSummary,
                IsDownloadPackageDetailAvailable = availablity[1],
                DownloadPackageVersionsSummary = _statisticsService.DownloadPackageVersionsSummary
            };

            return View(model);
        }

        //
        // GET: /stats/packages

        public virtual async Task<ActionResult> Packages()
        {
            if (_statisticsService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            bool isAvailable = await _statisticsService.LoadDownloadPackages();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = isAvailable,
                DownloadPackagesAll = _statisticsService.DownloadPackagesAll
            };

            return View(model);
        }

        //
        // GET: /stats/packageversions

        public virtual async Task<ActionResult> PackageVersions()
        {
            if (_statisticsService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            bool isAvailable = await _statisticsService.LoadDownloadPackageVersions();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageDetailAvailable = isAvailable,
                DownloadPackageVersionsAll = _statisticsService.DownloadPackageVersionsAll
            };

            return View(model);
        }

        //
        // GET: /stats/package/{id}

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id, string[] groupby)
        {
            if (_statisticsService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            StatisticsPackagesReport report = await _statisticsService.GetPackageDownloadsByVersion(id);

            if (report != null)
            {
                string[] pivot = new string[4];

                if (groupby != null)
                {
                    //  process and validate teh groupby query. unrecognized fields are ignored. others regarded for existance

                    int dimension = 0;

                    CheckGroupBy(groupby, "Version", pivot, ref dimension, report);
                    CheckGroupBy(groupby, "Client", pivot, ref dimension, report);
                    CheckGroupBy(groupby, "Operation", pivot, ref dimension, report);

                    if (dimension == 0)
                    {
                        //  no recognized fields so just fall into the null logic

                        groupby = null;
                    }
                    else
                    {
                        Array.Resize(ref pivot, dimension);
                    }

                    Tuple<StatisticsPivot.TableEntry[][], int> result = StatisticsPivot.GroupBy(report.Facts, pivot);

                    int col = Array.FindIndex(pivot, (s) => s.Equals("Version"));
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

                    report.Table = result.Item1;
                    report.Total = result.Item2;
                    report.Columns = pivot;
                }

                if (groupby == null)
                {
                    //  degenerate case (but still logically valid)

                    report.Dimensions.Add(new StatisticsDimension { Name = "Version", IsChecked = false });
                    report.Dimensions.Add(new StatisticsDimension { Name = "Client", IsChecked = false });
                    report.Dimensions.Add(new StatisticsDimension { Name = "Operation", IsChecked = false });

                    report.Table = null;
                    report.Total = StatisticsPivot.Total(report.Facts);
                }
            }

            StatisticsPackagesViewModel model = new StatisticsPackagesViewModel();

            model.SetPackageDownloadsByVersion(id, report);

            return View(model);
        }

        // the following should be dead now

        //
        // GET: /stats/package/{id}/{version}

        public virtual async Task<ActionResult> PackageDownloadsDetail(string id, string version)
        {
            if (_statisticsService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            StatisticsPackagesReport report = await _statisticsService.GetPackageVersionDownloadsByClient(id, version);

            var model = new StatisticsPackagesViewModel();

            model.SetPackageVersionDownloadsByClient(id, version, report);

            return View(model);
        }

        private static void CheckGroupBy(string[] groupby, string name, string[] pivot, ref int dimension, StatisticsPackagesReport report)
        {
            if (Array.Exists(groupby, (s) => s.Equals(name)))
            {
                pivot[dimension++] = name;
                report.Dimensions.Add(new StatisticsDimension { Name = name, IsChecked = true });
            }
            else
            {
                report.Dimensions.Add(new StatisticsDimension { Name = name, IsChecked = false });
            }
        }
    }
}
