// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Services;

namespace NuGetGallery.Areas.Admin.Controllers
{
	public class ForceValidationFailureController : AdminControllerBase
	{
		private readonly IPackageService _packageService;

		public ForceValidationFailureController(
			IPackageService packageService)
		{
			_packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
		}

		[HttpGet]
		public virtual ActionResult Index()
		{
			var model = new ForceValidationFailureViewModel();
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> ForceFailure(ForceValidationFailureViewModel viewModel)
		{
			if (!ModelState.IsValid)
			{
				return View("Index", viewModel);
			}

			try
			{
				// Find the package
				Package package;
				if (string.IsNullOrWhiteSpace(viewModel.PackageVersion))
				{
					// Find latest version
					package = _packageService.FindPackageByIdAndVersion(viewModel.PackageId, version: null);
				}
				else
				{
					// Find specific version
					package = _packageService.FindPackageByIdAndVersionStrict(viewModel.PackageId, viewModel.PackageVersion);
				}

				if (package == null)
				{
					TempData["ErrorMessage"] = $"Package '{viewModel.PackageId}' {(string.IsNullOrWhiteSpace(viewModel.PackageVersion) ? "" : $"version '{viewModel.PackageVersion}'")} not found.";
					return View("Index", viewModel);
				}

				// Check if package is in a valid state for this operation
				if (package.PackageStatusKey == PackageStatus.FailedValidation)
				{
					TempData["Message"] = $"Package '{package.Id}' version '{package.NormalizedVersion}' is already in FailedValidation status.";
					return View("Index", viewModel);
				}

				if (package.PackageStatusKey == PackageStatus.Available)
				{
					TempData["ErrorMessage"] = $"Package '{package.Id}' version '{package.NormalizedVersion}' is Available and cannot be transitioned to FailedValidation. Only packages in Validating status can be forced to FailedValidation.";
					return View("Index", viewModel);
				}

				if (package.PackageStatusKey == PackageStatus.Deleted)
				{
					TempData["ErrorMessage"] = $"Package '{package.Id}' version '{package.NormalizedVersion}' is Deleted and cannot be modified.";
					return View("Index", viewModel);
				}

                // Get the validation initiator
                var _packageValidationInitiator = DependencyResolver.Current.GetService<IPackageValidationInitiator<Package>>();

                // Call the ValidationInitiator to fail the validation
                var asynchronousInitiator = _packageValidationInitiator as AsynchronousPackageValidationInitiator<Package>;
                if (asynchronousInitiator != null)
                {
                    var resultStatus = await asynchronousInitiator.FailValidationAsync(package);

                    // Update the package status to reflect the failure
                    await _packageService.UpdatePackageStatusAsync(package, resultStatus, commitChanges: true);
                    TempData["SuccessMessage"] = $"Successfully forced package '{package.Id}' version '{package.NormalizedVersion}' to FailedValidation status.";
                }
                else
                {
                    // create an error message if the initiator is not available
                    TempData["ErrorMessage"] = $"Failed to force validation failure: The package validation initiator is not configured for asynchronous validation.";
                }

            }
			catch (Exception e)
			{
				TempData["ErrorMessage"] = $"Failed to force validation failure: {e.Message}";
			}

			return View("Index", viewModel);
		}
	}
}
