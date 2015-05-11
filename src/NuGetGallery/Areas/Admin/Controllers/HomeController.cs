// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class HomeController : AdminControllerBase
    {
        private readonly IContentService _content;

        public HomeController(IContentService content)
        {
            _content = content;
        }

        public virtual ActionResult Index()
        {
            return View();
        }

        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Throw", Justification="This is an admin action")]
        [SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "This is an admin action")]
        public virtual ActionResult Throw()
        {
            throw new Exception("KA BOOM!");
        }

        public virtual ActionResult ClearContentCache()
        {
            _content.ClearCache();
            TempData["Message"] = "Cleared Content Cache";
            return RedirectToAction("Index");
        }
    }
}