// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class HomeController : AdminControllerBase
    {
        private readonly IContentService _content;
        private readonly IGalleryConfigurationService _config;

        public HomeController(IContentService content, IGalleryConfigurationService config)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        [HttpGet]
        [ActionName(ActionName.AdminHomeIndex)]
        public virtual ActionResult Index()
        {
            var viewModel = new HomeViewModel(
                showValidation: _config.Current.AsynchronousPackageValidationEnabled);

            return View(viewModel);
        }

        [HttpGet]
        public virtual ActionResult Throw()
        {
            throw new Exception("KA BOOM!");
        }

        [HttpGet]
        [ActionName(ActionName.AdminClearContentCache)]
        public virtual ActionResult ClearContentCache()
        {
            _content.ClearCache();
            TempData["Message"] = "Cleared Content Cache";
            return RedirectToAction(ActionName.AdminHomeIndex);
        }
    }
}