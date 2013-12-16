using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Diagnostics
{
    public class TroubleShootingController : Controller
    {
        //
        // GET: /TroubleShooting/

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Details()
        {
            return PartialView("~/Views/TroubleShooting/TroubleShooting_Details.cshtml");
        }

    }
}
