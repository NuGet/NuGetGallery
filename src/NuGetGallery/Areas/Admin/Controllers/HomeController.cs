using System;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class HomeController : AdminControllerBase
    {
        public virtual ActionResult Index()
        {
            return View();
        }

        public virtual ActionResult Throw()
        {
            throw new Exception("KA BOOM!");
        }
    }
}