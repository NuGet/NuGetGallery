// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Features;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class FeaturesController : AdminControllerBase
    {
        private readonly IMutableFeatureFlagStorageService _storage;

        public FeaturesController(IMutableFeatureFlagStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        [HttpGet]
        public async virtual Task<ActionResult> Index()
        {
            var reference = await _storage.GetReferenceAsync();

            return View(nameof(Index), new FeatureFlagsViewModel
            {
                Flags = reference.Flags,
                ContentId = reference.ContentId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(FeatureFlagsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(Index), model);
            }

            var result = await _storage.TrySaveAsync(model.Flags, model.ContentId);

            switch (result)
            {
                case FeatureFlagSaveResult.Ok:
                    TempData["Message"] = "Your feature flags have been saved";
                    break;

                case FeatureFlagSaveResult.Invalid:
                    // This case shouldn't happen as the ModelState should be invalid.
                    TempData["ErrorMessage"] = "Could not save feature flags as they were malformed.";
                    break;

                case FeatureFlagSaveResult.Conflict:
                    TempData["ErrorMessage"] = "The feature flags were modified by someone else. Please try again.";
                    break;

                default:
                    TempData["ErrorMessage"] = $"Unknown save result '{result}'.";
                    break;
            }

            return Redirect(Url.Action(actionName: "Index", controllerName: "Features"));
        }
    }
}