﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SearchService.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            return View();
        }

        //
        // GET: /Segments/

        public ActionResult Segments()
        {
            return View();
        }

        //
        // GET: /RangeQuery/

        public ActionResult RangeQuery()
        {
            return View();
        }
    }
}
