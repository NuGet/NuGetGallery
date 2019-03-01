// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Configuration;
using NuGetGallery.Features;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class FeaturesController : AdminControllerBase
    {
        private readonly IEditableFeatureFlagStorageService _storage;
        private readonly IFeatureFlagCacheService _cache;
        private readonly IAppConfiguration _config;

        public FeaturesController(
            IEditableFeatureFlagStorageService storage,
            IFeatureFlagCacheService cache,
            IAppConfiguration config)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        [HttpGet]
        [ActionName(ActionName.AdminFeatureFlags)]
        public async virtual Task<ActionResult> Index()
        {
            var reference = await _storage.GetReferenceAsync();
            var lastUpdated = _cache.GetRefreshTimeOrNull();

            TimeSpan? timeSinceLastRefresh = null;
            if (lastUpdated.HasValue)
            {
                timeSinceLastRefresh = DateTimeOffset.UtcNow.Subtract(lastUpdated.Value);
            }

            return View(new FeatureFlagsViewModel
            {
                TimeSinceLastRefresh = timeSinceLastRefresh,
                RefreshInterval = _config.FeatureFlagsRefreshInterval,
                Flags = reference.FlagsJson,
                ContentId = reference.ContentId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName(ActionName.AdminFeatureFlags)]
        public async Task<ActionResult> Index(FeatureFlagsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _storage.TrySaveAsync(model.Flags, model.ContentId);

                switch (result.Type)
                {
                    case FeatureFlagSaveResultType.Ok:
                        // The flags have been persisted. Refresh this instance's cache immediately.
                        await _cache.RefreshAsync();

                        var refreshSeconds = _config.FeatureFlagsRefreshInterval.TotalSeconds;

                        TempData["Message"] = $"Your feature flags have been saved! It may take up to {refreshSeconds} seconds for this change to propagate everywhere.";
                        return Redirect(Url.Action(actionName: ActionName.AdminFeatureFlags, controllerName: "Features"));

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