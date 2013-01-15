using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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

        public virtual ActionResult Index()
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            model.LoadDownloadPackages();
            model.LoadDownloadPackageVersions();

            return View(model);
        }

        //
        // GET: /statistics/packages

        public virtual ActionResult Packages()
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            model.LoadDownloadPackages();

            return View(model);
        }

        //
        // GET: /statistics/packageversions

        public virtual ActionResult PackageVersions()
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            model.LoadDownloadPackageVersions();

            return View(model);
        }

        //
        // GET: /statistics/package/{id}

        public virtual ActionResult PackageDownloadsByVersion(string id)
        {
            var model = new StatisticsPackagesViewModel(_statisticsService);
            model.LoadPackageDownloadsByVersion(id);

            return View(model);
        }
    }
}
