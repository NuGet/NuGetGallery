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
        private readonly IEditableFeatureFlagStorageService _storage;

        public FeaturesController(IEditableFeatureFlagStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        [HttpGet]
        public async virtual Task<ActionResult> Index()
        {
            var reference = await _storage.GetReferenceAsync();

            return View(new FeatureFlagsViewModel
            {
                Flags = reference.FlagsJson,
                ContentId = reference.ContentId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(FeatureFlagsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _storage.TrySaveAsync(model.Flags, model.ContentId);

                switch (result.Type)
                {
                    case FeatureFlagSaveResultType.Ok:
                        TempData["Message"] = "Your feature flags have been saved!";
                        return Redirect(Url.Action(actionName: "Index", controllerName: "Features"));

                    case FeatureFlagSaveResultType.Conflict:
                        TempData["ErrorMessage"] = "Your changes were not applied as the feature flags were modified by someone else. " +
                            "Please reload the page and try again.";
                        break;

                    case FeatureFlagSaveResultType.Invalid:
                        ModelState.AddModelError(nameof(model.Flags), $"Invalid flags: {result.Message}");
                        break;

                    default:
                        TempData["ErrorMessage"] = $"Unknown save result '{result}': {result.Message}.";
                        break;
                }
            }

            return View(model);
        }
    }
}