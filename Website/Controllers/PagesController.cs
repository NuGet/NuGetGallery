using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class PagesController : Controller
    {
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
    }
}
