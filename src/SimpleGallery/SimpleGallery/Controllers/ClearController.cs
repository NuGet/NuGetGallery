using SimpleGalleryLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace SimpleGallery.Controllers
{
    public class ClearController : Controller
    {
        // GET: Clear
        public ActionResult Index()
        {
            SimpleGalleryAPI.DeleteCatalogItems();

            return View();
        }
    }
}