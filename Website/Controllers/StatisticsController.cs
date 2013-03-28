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
        [OutputCache(VaryByParam = "None", Duration = 120, Location = OutputCacheLocation.Server)]
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
            CultureInfo clientCulture = null;

            string[] languages = Request.UserLanguages;
            if (languages != null && languages.Length > 0)
            {
                try
                {
                    clientCulture = CultureInfo.GetCultureInfo(languages[0].ToLowerInvariant().Trim());
                }
                catch (CultureNotFoundException)
                {
                }
            }

            return clientCulture;
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

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id)
        {
            if (_statisticsService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            StatisticsPackagesReport report = await _statisticsService.GetPackageDownloadsByVersion(id);

            var model = new StatisticsPackagesViewModel();

            model.SetPackageDownloadsByVersion(id, report);

            return View(model);
        }

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
    }
}
