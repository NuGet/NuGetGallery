using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class HomeController : AdminControllerBase
    {
        public virtual ActionResult Index()
        {
            return View();
        }
    }
}