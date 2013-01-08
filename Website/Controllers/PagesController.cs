using System.Globalization;
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
    }
}