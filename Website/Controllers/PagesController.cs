using System.Web.Mvc;
using System.Web.UI;

namespace NuGetGallery
{
    public partial class PagesController : Controller
    {
        private readonly IAggregateStatsService _statsService;

        public PagesController(IAggregateStatsService statsService)
        {
            _statsService = statsService;
        }

        public virtual ActionResult Home()
        {
            return View();
        }

        public virtual ActionResult Terms()
        {
            return View();
        }

        public virtual ActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        [OutputCache(VaryByParam = "None", Duration = 120, Location = OutputCacheLocation.Server)]
        public virtual JsonResult Stats()
        {
            var stats = _statsService.GetAggregateStats();
            return Json(
                new
                    {
                        Downloads = stats.Downloads.ToString("n0"),
                        UniquePackages = stats.UniquePackages.ToString("n0"),
                        TotalPackages = stats.TotalPackages.ToString("n0")
                    },
                JsonRequestBehavior.AllowGet);
        }
    }
}
