using System.Web.Mvc;
using System.Web.UI;

namespace NuGetGallery
{
    public partial class PagesController : Controller
    {
        private readonly IAggregateStatsService statsSvc;

        public PagesController(IAggregateStatsService statsSvc)
        {
            this.statsSvc = statsSvc;
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
            var stats = statsSvc.GetAggregateStats();
            return Json(new
            {
                Downloads = stats.Downloads.ToString("n0"),
                UniquePackages = stats.UniquePackages.ToString("n0"),
                TotalPackages = stats.TotalPackages.ToString("n0")
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
