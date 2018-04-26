// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;

namespace NuGet.Services.CDNRedirect.Controllers
{
    public class StatusController : Controller
    {
        // GET: Status
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }
    }
}