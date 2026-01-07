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
        public virtual ActionResult Index()
        {
            var viewModel = new HomeViewModel(
                showDatabaseAdmin: _config.Current.AdminPanelDatabaseAccessEnabled,
                showLuceneAdmin: _config.Current.SearchServiceUriPrimary == null && _config.Current.SearchServiceUriSecondary == null,
                showValidation: _config.Current.AsynchronousPackageValidationEnabled);

            return View(viewModel);
        }

        [HttpGet]
        public virtual ActionResult Throw()
        {
            throw new Exception("KA BOOM!");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ClearContentCache()
        {
            _content.ClearCache();
            TempData["Message"] = "Cleared Content Cache";
            return RedirectToAction("Index");
        }
    }
}