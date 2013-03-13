using System.Globalization;
using System.Web.Mvc;
using System.Web.UI;

namespace NuGetGallery
{
    public partial class PagesController : Controller
    {
        public PagesController()
        {
        }

        public virtual ActionResult Contact()
        {
            return View();
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
    }
}