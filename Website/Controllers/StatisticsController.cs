using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using NuGetGallery.Commands;
using NuGetGallery.Statistics;
using NuGetGallery.ViewModels;

namespace NuGetGallery
{
    public partial class StatisticsController : NuGetControllerBase
    {
        public StatisticsController(ICommandExecutor executor) : base(executor) { }

        [HttpGet]
        [OutputCache(VaryByHeader = "Accept-Language", Duration = 120, Location = OutputCacheLocation.Server)]
        public virtual ActionResult Totals()
        {
            var stats = 
                Executor.ExecuteAndCatch(new AggregateStatsCommand()) ??
                new AggregateStats();
            
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

        //
        // GET: /stats

        public virtual async Task<ActionResult> Index()
        {
            var reports = await Executor.ExecuteAndCatchAsyncAll(
                new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads),
                new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads));

            // ExecuteAsyncAll returns results in the same order as the tasks.
            return View(new StatisticsSummaryViewModel(
                reports[0] ?? PackageDownloadsReport.Empty, 
                reports[1] ?? PackageDownloadsReport.Empty));
        }

        //
        // GET: /stats/packages

        public virtual async Task<ActionResult> Packages()
        {
            var report = await Executor.ExecuteAndCatchAsync(
                new PackageDownloadsReportCommand(
                    ReportNames.RecentPackageDownloads));
            return View(report ?? PackageDownloadsReport.Empty);
        }

        //
        // GET: /stats/packageversions

        public virtual async Task<ActionResult> PackageVersions()
        {
            var report = await Executor.ExecuteAndCatchAsync(
                new PackageDownloadsReportCommand(
                    ReportNames.RecentPackageVersionDownloads));
            return View(report ?? PackageDownloadsReport.Empty);
        }

        //
        // GET: /stats/package/{id}

        public virtual async Task<ActionResult> PackageDownloadsById(string id, string[] groupby)
        {
            var report = await Executor.Execute(new PackageDownloadDetailReportCommand(id));
            ProcessReport(report, groupby, new string[] { "Version", "ClientName", "ClientVersion", "Operation" }, id);
            return View(new PivotableStatisticsReportViewModel(id, report));
        }

        //
        // GET: /stats/package/{id}/{version}

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id, string version, string[] groupby)
        {
            var report = await Executor.Execute(new PackageDownloadDetailReportCommand(id, version));
            ProcessReport(report, groupby, new string[] { "ClientName", "ClientVersion", "Operation" });
            return View(new PivotableStatisticsReportViewModel(id, version, report));
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

        private void ProcessReport(DownloadStatisticsReport report, string[] groupby, string[] dimensions, string id = null)
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

                Tuple<StatisticsPivot.TableEntry[][], int> result = StatisticsPivot.GroupBy(report.Facts, pivot);

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
                report.Total = report.Facts.Sum(fact => fact.Amount);
            }
        }

        private static void CheckGroupBy(string[] groupby, string name, string[] pivot, ref int dimension, DownloadStatisticsReport report)
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
    }
}
