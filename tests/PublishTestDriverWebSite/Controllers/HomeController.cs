// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using PublishTestDriverWebSite.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PublishTestDriverWebSite.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            HomeModel model = new HomeModel();

            model.ClientId = ConfigurationManager.AppSettings["ida:ClientId"];
            model.AADInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
            model.Tenant = ConfigurationManager.AppSettings["ida:Tenant"];
            model.CertificateThumbprint = Startup.Thumbprint;

            if (Startup.Certificate != null)
            {
                model.CertificateSubject = Startup.Certificate.Subject;
            }
            else
            {
                model.CertificateSubject = "(null)";
            }

            return View(model);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}