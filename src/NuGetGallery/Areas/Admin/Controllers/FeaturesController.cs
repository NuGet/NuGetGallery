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
            return MergeChangesAndTrySave(
                feature,
                GetAdd(
                    feature,
                    ValidateFeature,
                    GetFeatures,
                    PrettyFeatureName));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> EditFeature(ModifyFeatureFlagsFeatureViewModel feature)
        {
            return MergeChangesAndTrySave(
                feature,
                GetEdit(
                    feature,
                    ValidateFeature,
                    GetFeatures,
                    (existingFeatures, existingFeature) =>
                    {
                        existingFeature.Status = feature.Status;

                        return null;
                    },
                    PrettyFeatureName));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> DeleteFeature(ModifyFeatureFlagsFeatureViewModel feature)
        {
            return MergeChangesAndTrySave(
                feature,
                GetDelete(
                    feature,
                    ValidateFeature,
                    GetFeatures,
                    PrettyFeatureName));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> AddFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            return MergeChangesAndTrySave(
                flight,
                GetAdd(
                    flight,
                    ValidateFlight,
                    GetFlights,
                    PrettyFlightName));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> EditFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            return MergeChangesAndTrySave(
                flight,
                GetEdit(
                    flight,
                    ValidateFlight,
                    GetFlights,
                    (existingFlights, existingFlight) =>
                    {
                        existingFlight.All = flight.All;
                        existingFlight.SiteAdmins = flight.SiteAdmins;
                        existingFlight.Accounts = flight.Accounts;
                        existingFlight.Domains = flight.Domains;

                        return null;
                    },
                    PrettyFlightName));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> DeleteFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            return MergeChangesAndTrySave(
                flight,
                GetDelete(
                    flight,
                    ValidateFlight,
                    GetFlights,
                    PrettyFlightName));
        }

        private delegate string ApplyChange(FeatureFlagsViewModel model);

        private const string PrettyFeatureName = "feature";
        private const string PrettyFlightName = "flight";

        private ApplyChange GetAdd<TModify, TBase>(
            TModify change,
            ValidateChanges<TModify> validateChanges,
            GetExistingList<TBase> getExistingList,
            string prettyChangeName)
            where TModify : class, IModifyFeatureFlagsViewModel, TBase
            where TBase : class, IFeatureFlagsViewModel
        {
            return GetApplyChange(
                change,
                validateChanges,
                getExistingList,
                (existingList, existing) => $"The {prettyChangeName} '{change.Name}' already exists. You cannot add a {prettyChangeName} that already exists.",
                existingList =>
                {
                    existingList.Add(change);
                    return null;
                });
        }

        private ApplyChange GetEdit<TModify, TBase>(
            TModify change,
            ValidateChanges<TModify> validateChanges,
            GetExistingList<TBase> getExistingList,
            HandleExisting<TBase> editExisting,
            string prettyChangeName)
            where TModify : class, IModifyFeatureFlagsViewModel, TBase
            where TBase : class, IFeatureFlagsViewModel
        {
            return GetApplyChange(
                change,
                validateChanges,
                getExistingList,
                editExisting,
                existingList => $"The {prettyChangeName} '{change.Name}' does not exist. You cannot edit a {prettyChangeName} that does not exist.");
        }

        private ApplyChange GetDelete<TModify, TBase>(
            TModify change,
            ValidateChanges<TModify> validateChanges,
            GetExistingList<TBase> getExistingList,
            string prettyChangeName)
            where TModify : class, IModifyFeatureFlagsViewModel, TBase
            where TBase : class, IFeatureFlagsViewModel
        {
            return GetApplyChange(
                change,
                validateChanges,
                getExistingList,
                (existingList, existing) =>
                {
                    existingList.Remove(existing);
                    return null;
                },
                existingList => $"The {prettyChangeName} '{change.Name}' does not exist. You cannot delete a {prettyChangeName} that does not exist.");
        }

        private ApplyChange GetApplyChange<TModify, TBase>(
            TModify change,
            ValidateChanges<TModify> validateChanges, 
            GetExistingList<TBase> getExistingList, 
            HandleExisting<TBase> handleExisting, 
            HandleMissing<TBase> handleMissing)
            where TModify : class, IModifyFeatureFlagsViewModel, TBase
            where TBase : class, IFeatureFlagsViewModel
        {
            return (model) =>
            {
                var validationError = validateChanges(change);
                if (validationError != null)
                {
                    return validationError;
                }

                var existingList = getExistingList(model);
                var existing = existingList.SingleOrDefault(f => f.Name == change.Name);
                return existing == null ? handleMissing(existingList) : handleExisting(existingList, existing);
            };
        }

        private delegate string ValidateChanges<TModify>(TModify change);

        /// <remarks>
        /// There are no validations on features yet.
        /// </remarks>
        private string ValidateFeature(ModifyFeatureFlagsFeatureViewModel feature)
        {
            return null;
        }

        private string ValidateFlight(ModifyFeatureFlagsFlightViewModel flight)
        {
            if (flight.Accounts?.Any() ?? false)
            {
                var missingAccounts = new List<string>();
                foreach (var accountName in flight.Accounts)
                {
                    var user = _userService.FindByUsername(accountName);
                    if (user == null)
                    {
                        missingAccounts.Add(accountName);
                    }
                }

                if (missingAccounts.Any())
                {
                    return $"Some accounts specified by the flight '{flight.Name}' ({string.Join(", ", missingAccounts)}) do not exist. A flight cannot specify accounts that do not exist.";
                }
            }

            return null;
        }

        private delegate List<TBase> GetExistingList<TBase>(FeatureFlagsViewModel model);

        private List<FeatureFlagsFeatureViewModel> GetFeatures(FeatureFlagsViewModel model)
        {
            return model.Features;
        }

        private List<FeatureFlagsFlightViewModel> GetFlights(FeatureFlagsViewModel model)
        {
            return model.Flights;
        }

        private delegate string HandleExisting<TBase>(List<TBase> existingList, TBase existing);
        private delegate string HandleMissing<TBase>(List<TBase> existingList);

        private async Task<ActionResult> MergeChangesAndTrySave<T>(T changes, ApplyChange applyChange)
            where T : IModifyFeatureFlagsViewModel
        {
            var model = await GetModel();

            var errorMessage = ValidateModelState() ?? applyChange(model) ?? await TrySaveFlags(model, changes.ContentId);
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
                .Where(e => !string.IsNullOrWhiteSpace(e.ErrorMessage))
                .Select(e => e.ErrorMessage);

            return errorMessages.Any()
                ? "The model submitted was invalid: " + string.Join(" ", errorMessages)
                : "The model submitted was invalid.";
        }

        private async Task<string> TrySaveFlags(FeatureFlagsViewModel model, string contentId)
        {
            var flags = new FeatureFlags(
                model.Features.ToDictionary(f => f.Name, f => f.Status),
                model.Flights.ToDictionary(f => f.Name, GetFlightFromModel));

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

        private Flight GetFlightFromModel(FeatureFlagsFlightViewModel flight)
        {
            return new Flight(
                flight.All,
                flight.SiteAdmins,
                flight.Accounts?.ToList() ?? new List<string>(),
                flight.Domains?.ToList() ?? new List<string>());
        }
    }
}