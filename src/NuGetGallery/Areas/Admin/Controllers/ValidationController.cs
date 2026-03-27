// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ValidationController : AdminControllerBase
    {
        private readonly ValidationAdminService _validationAdminService;
        private readonly IPackageService _packageService;
        private readonly IPackageValidationInitiator<Package> _packageValidationInitiator;

        public ValidationController(
            ValidationAdminService validationAdminService,
            IPackageService packageService,
            IPackageValidationInitiator<Package> packageValidationInitiator)
        {
            _validationAdminService = validationAdminService ?? throw new ArgumentNullException(nameof(validationAdminService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageValidationInitiator = packageValidationInitiator ?? throw new ArgumentNullException(nameof(packageValidationInitiator));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            return View(nameof(Index), new ValidationPageViewModel());
        }

        [HttpGet]
        public virtual ActionResult Pending()
        {
            var validationSets = _validationAdminService.GetPending();
            var packageValidations = ToPackageValidations(validationSets);
            var validationSetIds = packageValidations
                .SelectMany(p => p.ValidationSets)
                .Select(s => s.ValidationTrackingId);
            var query = string.Join("\r\n", validationSetIds);

            return View(nameof(Index), new ValidationPageViewModel(query, packageValidations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<RedirectToRouteResult> RevalidatePending(ValidatingType validatingType)
        {
            var revalidatedCount = await _validationAdminService.RevalidatePendingAsync(validatingType);

            if (revalidatedCount == 0)
            {
                TempData["Message"] = $"There are no {validatingType} instances that are in the {PackageStatus.Validating} state so no validations were enqueued.";
            }
            else
            {
                TempData["Message"] = $"{revalidatedCount} validations were enqueued for {validatingType} instances that are in the {PackageStatus.Validating} state. " +
                    "It may take some time for the new validations to appear as the validation subsystem reacts to the enqueued messages.";
            }

            return RedirectToAction(nameof(Pending));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ForceFailValidation (string packageId, string packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                TempData["ErrorMessage"] = "Package ID is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                Package package;
                if (string.IsNullOrWhiteSpace(packageVersion))
                {
                    package = _packageService.FindPackageByIdAndVersion(packageId, version: null);
                }
                else
                {
                    package = _packageService.FindPackageByIdAndVersionStrict(packageId, packageVersion);
                }

                if (package == null)
                {
                    TempData["ErrorMessage"] = $"Package '{packageId}' {(string.IsNullOrWhiteSpace(packageVersion) ? "" : $"version '{packageVersion}' ")}not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (package.PackageStatusKey == PackageStatus.FailedValidation)
                {
                    TempData["Message"] = $"Package '{package.Id}' version '{package.NormalizedVersion}' is already in FailedValidation status.";
                    return RedirectToAction(nameof(Index));
                }

                if (package.PackageStatusKey == PackageStatus.Available)
                {
                    TempData["ErrorMessage"] = $"Package '{package.Id}' version '{package.NormalizedVersion}' is Available and cannot be transitioned to FailedValidation. Only packages in Validating status can be forced to FailedValidation.";
                    return RedirectToAction(nameof(Index));
                }

                if (package.PackageStatusKey == PackageStatus.Deleted)
                {
                    TempData["ErrorMessage"] = $"Package '{package.Id}' version '{package.NormalizedVersion}' is Deleted and cannot be modified.";
                    return RedirectToAction(nameof(Index));
                }

                var asynchronousInitiator = _packageValidationInitiator as AsynchronousPackageValidationInitiator<Package>;
                if (asynchronousInitiator != null)
                {
                    var resultStatus = await asynchronousInitiator.FailValidationAsync(package);
                    await _packageService.UpdatePackageStatusAsync(package, resultStatus, commitChanges: true);
                    TempData["Message"] = $"Successfully forced package '{package.Id}' version '{package.NormalizedVersion}' to FailedValidation status.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to force validation failure: The package validation initiator is not configured for asynchronous validation.";
                }
            }
            catch (Exception e)
            {
                TempData["ErrorMessage"] = $"Failed to force validation failure: {e.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public virtual ActionResult Search(string q)
        {
            var packageValidationSets = _validationAdminService.Search(q ?? string.Empty);
            var validatedPackages = ToPackageValidations(packageValidationSets);

            return View(nameof(Index), new ValidationPageViewModel(q, validatedPackages));
        }

        private List<NuGetPackageValidationViewModel> ToPackageValidations(IReadOnlyList<PackageValidationSet> packageValidationSets)
        {
            // TODO: Add generic validation sets.
            // Tracked by: https://github.com/NuGet/Engineering/issues/3587
            var packageValidations = new List<NuGetPackageValidationViewModel>();
            AppendNuGetPackageValidations(packageValidations, packageValidationSets, ValidatingType.Package);
            AppendNuGetPackageValidations(packageValidations, packageValidationSets, ValidatingType.SymbolPackage);

            return packageValidations;
        }

        private void AppendNuGetPackageValidations(
            List<NuGetPackageValidationViewModel> packageValidations,
            IEnumerable<PackageValidationSet> validationSets,
            ValidatingType validatingType)
        {
            var groups = validationSets
                .Where(x => x.ValidatingType == validatingType)
                .OrderBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(x => NuGetVersion.Parse(x.PackageNormalizedVersion))
                .ThenByDescending(x => x.PackageKey)
                .ThenByDescending(x => x.Created)
                .ThenByDescending(x => x.Key)
                .GroupBy(x => x.PackageKey);

            foreach (var group in groups)
            {
                foreach (var set in group)
                {
                    set.PackageValidations = set.PackageValidations
                        .OrderBy(x => x.Started)
                        .ToList();
                }
                var deletedStatus = _validationAdminService.GetDeletedStatus(group.Key.Value, validatingType);
                var packageValidation = new NuGetPackageValidationViewModel(group.ToList(), deletedStatus, validatingType);
                packageValidations.Add(packageValidation);
            }
        }
    }
}