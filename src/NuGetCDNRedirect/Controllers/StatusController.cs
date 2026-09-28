// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace NuGet.Services.CDNRedirect.Controllers
{
    public class StatusController : Controller
    {
        [HttpGet("/Status/Index")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
