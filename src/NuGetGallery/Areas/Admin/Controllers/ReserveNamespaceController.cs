using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ReserveNamespaceController : AdminControllerBase
    {

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public virtual JsonResult SearchPrefix(string query)
        {
            var prefixList = new string[] { "abc", "abc.*", "Microsoft.*" };
            var obj = new
            {
                Prefixes = prefixList.Select(p => new { Prefix = p })
            };

            return Json(obj, JsonRequestBehavior.AllowGet);
        }
    }
}