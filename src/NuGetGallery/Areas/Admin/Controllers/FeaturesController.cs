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

            return View(nameof(Index), new FeatureFlagsViewModel(reference.Flags, reference.ContentId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Update(string flags, string contentId)
        {
            var result = await _storage.TrySaveAsync(flags, contentId);

            switch (result)
            {
                case FeatureFlagSaveResult.Ok:
                    TempData["Message"] = "Your feature flags have been saved";
                    break;

                case FeatureFlagSaveResult.Invalid:
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