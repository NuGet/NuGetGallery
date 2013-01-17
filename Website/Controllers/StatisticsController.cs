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
            var model = new StatisticsPackagesViewModel(_statisticsService);
            await Task.WhenAll(model.LoadDownloadPackages(), model.LoadDownloadPackageVersions());
            return View(model);
        }

        //
        // GET: /statistics/packages

        public virtual async Task<ActionResult> Packages()
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            await model.LoadDownloadPackages();
            return View(model);
        }

        //
        // GET: /statistics/packageversions

        public virtual async Task<ActionResult> PackageVersions()
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            await model.LoadDownloadPackageVersions();
            return View(model);
        }

        //
        // GET: /statistics/package/{id}

        public virtual async Task<ActionResult> PackageDownloadsByVersion(string id)
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            await model.LoadPackageDownloadsByVersion(id);
            return View(model);
        }
    }
}
