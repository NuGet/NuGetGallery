// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IUserService _userService;
        private readonly IEditableFeatureFlagStorageService _storage;
        private readonly IFeatureFlagCacheService _cache;
        private readonly IAppConfiguration _config;

        public FeaturesController(
            IUserService userService,
            IEditableFeatureFlagStorageService storage,
            IFeatureFlagCacheService cache,
            IAppConfiguration config)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        [HttpGet]
        public async virtual Task<ActionResult> Index()
        {
            return View(await GetModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> AddFeature(ModifyFeatureFlagsFeatureViewModel feature)
        {
            return MergeChangesAndTrySave<ModifyFeatureFlagsFeatureViewModel, FeatureFlagsFeatureViewModel>(
                feature,
                ChangeType.Add);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> EditFeature(ModifyFeatureFlagsFeatureViewModel feature)
        {
            return MergeChangesAndTrySave<ModifyFeatureFlagsFeatureViewModel, FeatureFlagsFeatureViewModel>(
                feature,
                ChangeType.Edit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> DeleteFeature(ModifyFeatureFlagsFeatureViewModel feature)
        {
            return MergeChangesAndTrySave<ModifyFeatureFlagsFeatureViewModel, FeatureFlagsFeatureViewModel>(
                feature,
                ChangeType.Delete);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> AddFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            return MergeChangesAndTrySave<ModifyFeatureFlagsFlightViewModel, FeatureFlagsFlightViewModel>(
                flight,
                ChangeType.Add);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> EditFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            return MergeChangesAndTrySave<ModifyFeatureFlagsFlightViewModel, FeatureFlagsFlightViewModel>(
                flight,
                ChangeType.Edit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> DeleteFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            return MergeChangesAndTrySave<ModifyFeatureFlagsFlightViewModel, FeatureFlagsFlightViewModel>(
                flight,
                ChangeType.Delete);
        }

        private string ApplyChange<TModify, TBase>(
            FeatureFlagsViewModel model,
            TModify change,
            ChangeType type)
            where TModify : IModifyFeatureFlagsViewModel<TBase>, TBase
            where TBase : IFeatureFlagsViewModel
        {
            var validationError = change.GetValidationError(_userService);
            if (validationError != null)
            {
                return validationError;
            }

            var existingList = change.GetExistingList(model);
            var existing = existingList.SingleOrDefault(f => f.Name == change.Name);
            switch (type)
            {
                case ChangeType.Add:
                    if (existing == null)
                    {
                        existingList.Add(change);
                        return null;
                    }
                    else
                    {
                        return $"The {change.PrettyName} '{change.Name}' already exists. " +
                            $"You cannot add a {change.PrettyName} that already exists.";
                    }

                case ChangeType.Edit:
                    if (existing == null)
                    {
                        return $"The {change.PrettyName} '{change.Name}' does not exist. " +
                            $"You cannot edit a {change.PrettyName} that does not exist.";
                    }
                    else
                    {
                        change.ApplyTo(existing);
                        return null;
                    }

                case ChangeType.Delete:
                    if (existing == null)
                    {
                        return $"The {change.PrettyName} '{change.Name}' does not exist. " +
                            $"You cannot delete a {change.PrettyName} that does not exist.";
                    }
                    else
                    {
                        existingList.Remove(existing);
                        return null;
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private async Task<ActionResult> MergeChangesAndTrySave<TModify, TBase>(
            TModify change,
            ChangeType type)
            where TModify : IModifyFeatureFlagsViewModel<TBase>, TBase
            where TBase : IFeatureFlagsViewModel
        {
            var model = await GetModel();

            var errorMessage = ValidateModelState() 
                ?? ApplyChange<TModify, TBase>(model, change, type) 
                ?? await TrySaveFlags(model, change.ContentId);
            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
            }

            return RedirectToAction(nameof(Index));
        }

        private string ValidateModelState()
        {
            if (ModelState.IsValid)
            {
                return null;
            }

            var errorMessages = ModelState
                .SelectMany(s => s.Value.Errors)
                .Select(e => e.ErrorMessage)
                .Where(e => !string.IsNullOrWhiteSpace(e));

            return errorMessages.Any()
                ? "The model submitted was invalid: " + string.Join(" ", errorMessages)
                : "The model submitted was invalid.";
        }

        private async Task<string> TrySaveFlags(FeatureFlagsViewModel model, string contentId)
        {
            var flags = new FeatureFlags(
                model.Features.ToDictionary(f => f.Name, f => f.Status),
                model.Flights.ToDictionary(f => f.Name, f => f.AsFlight()));

            var result = await _storage.TrySaveAsync(flags, contentId);

            switch (result.Type)
            {
                case FeatureFlagSaveResultType.Ok:
                    // The flags have been persisted. Refresh this instance's cache immediately.
                    await _cache.RefreshAsync();

                    var refreshSeconds = _config.FeatureFlagsRefreshInterval.TotalSeconds;
                    TempData["Message"] = $"Your feature flags have been saved! It may take up to {refreshSeconds} seconds for this change to propagate everywhere.";
                    return null;

                case FeatureFlagSaveResultType.Conflict:
                    return "Your changes were not applied as the feature flags were modified by someone else. Please reload the page and try again.";

                default:
                    return $"Unknown save result '{result}': {result.Message}.";
            }
        }

        private enum ChangeType
        {
            Add,
            Edit,
            Delete
        }

        private async Task<FeatureFlagsViewModel> GetModel()
        {
            var reference = await _storage.GetReferenceAsync();
            var lastUpdated = _cache.GetRefreshTimeOrNull();

            TimeSpan? timeSinceLastRefresh = null;
            if (lastUpdated.HasValue)
            {
                timeSinceLastRefresh = DateTimeOffset.UtcNow.Subtract(lastUpdated.Value);
            }

            return new FeatureFlagsViewModel
            {
                TimeSinceLastRefresh = timeSinceLastRefresh,
                RefreshInterval = _config.FeatureFlagsRefreshInterval,
                Features = GetFeaturesFromFlags(reference.Flags),
                Flights = GetFlightsFromFlags(reference.Flags),
                ContentId = reference.ContentId
            };
        }

        private List<FeatureFlagsFeatureViewModel> GetFeaturesFromFlags(FeatureFlags flags)
        {
            return flags.Features.Select(f => new FeatureFlagsFeatureViewModel(f.Key, f.Value)).ToList();
        }

        private List<FeatureFlagsFlightViewModel> GetFlightsFromFlags(FeatureFlags flags)
        {
            return flags.Flights.Select(f => new FeatureFlagsFlightViewModel(f.Key, f.Value)).ToList();
        }
    }
}