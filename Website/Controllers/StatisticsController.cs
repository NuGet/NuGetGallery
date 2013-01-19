using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class StatisticsController : Controller
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        //
        // GET: /Statistics/

        public virtual async Task<ActionResult> Index()
        {
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
        // GET: /statistics/packages

        public virtual async Task<ActionResult> Packages()
        {
            bool isAvailable = await _statisticsService.LoadDownloadPackages();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageAvailable = isAvailable,
                DownloadPackagesAll = _statisticsService.DownloadPackagesAll
            };

            return View(model);
        }

        //
        // GET: /statistics/packageversions

        public virtual async Task<ActionResult> PackageVersions()
        {
            bool isAvailable = await _statisticsService.LoadDownloadPackageVersions();

            var model = new StatisticsPackagesViewModel
            {
                IsDownloadPackageDetailAvailable = isAvailable,
                DownloadPackageVersionsAll = _statisticsService.DownloadPackageVersionsAll
            };

            return View(model);
        }

        //
        // GET: /statistics/package/{id}

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id)
        {
            await _statisticsService.LoadPackageDownloadsByVersion(id);

            var model = new StatisticsPackagesViewModel();

            model.SetPackageDownloadsByVersion(id, _statisticsService.PackageDownloadsByVersion);

            return View(model);
        }
    }
}
