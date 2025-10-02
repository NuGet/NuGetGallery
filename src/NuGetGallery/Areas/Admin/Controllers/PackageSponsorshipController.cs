// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class PackageSponsorshipController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly ISponsorshipUrlService _sponsorshipUrlService;

        public PackageSponsorshipController(
            IPackageService packageService,
            ISponsorshipUrlService sponsorshipUrlService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _sponsorshipUrlService = sponsorshipUrlService ?? throw new ArgumentNullException(nameof(sponsorshipUrlService));
        }

        [HttpGet]
        public ActionResult Index(string packageId = null, string message = null, bool isSuccess = false)
        {
            var model = new PackageSponsorshipIndexViewModel
            {
                PackageId = packageId,
                Message = message,
                IsSuccess = isSuccess
            };

            if (!string.IsNullOrEmpty(packageId))
            {
                var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
                if (packageRegistration != null)
                {
                    // Get the latest version of the package
                    var package = packageRegistration.Packages
                        .OrderByDescending(p => NuGetVersion.Parse(p.NormalizedVersion))
                        .FirstOrDefault();
                    if (package != null)
                    {
                        model.Package = package;
                        // Use the service to get full sponsorship URL entries for admin display
                        model.SponsorshipUrls = _sponsorshipUrlService.GetSponsorshipUrlEntries(packageRegistration);
                    }
                }
                else
                {
                    model.Message = $"Package '{packageId}' not found.";
                    model.IsSuccess = false;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddUrl(string packageId, string newSponsorshipUrl)
        {
            try
            {
                if (!TryGetPackageForOperation(packageId, out var packageRegistration, out var errorResult))
                {
                    return errorResult;
                }

                var currentUser = GetCurrentUser();
                // Note: currentUser null check is handled by controller authorization

                // Add the sponsorship URL
                var validatedUrl = await _sponsorshipUrlService.AddSponsorshipUrlAsync(packageRegistration, newSponsorshipUrl, currentUser);
                return RedirectToAction("Index", new { packageId, message = "Sponsorship URL added successfully.", isSuccess = true });
            }
            catch (Exception ex)
            {
                return HandleSponsorshipUrlException(packageId, ex, "adding");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveUrl(string packageId, string sponsorshipUrl)
        {
            try
            {
                if (!TryGetPackageForOperation(packageId, out var packageRegistration, out var errorResult))
                {
                    return errorResult;
                }

                var currentUser = GetCurrentUser();
                // Note: currentUser null check is handled by controller authorization

                // Remove the sponsorship URL
                await _sponsorshipUrlService.RemoveSponsorshipUrlAsync(packageRegistration, sponsorshipUrl, currentUser);
                return RedirectToAction("Index", new { packageId, message = "Sponsorship URL removed successfully.", isSuccess = true });
            }
            catch (Exception ex)
            {
                return HandleSponsorshipUrlException(packageId, ex, "removing");
            }
        }

        /// <summary>
        /// Common package validation logic for sponsorship operations
        /// </summary>
        private bool TryGetPackageForOperation(string packageId, out PackageRegistration packageRegistration, out ActionResult errorResult)
        {
            packageRegistration = null;
            errorResult = null;

            packageRegistration = _packageService.FindPackageRegistrationById(packageId);
            if (packageRegistration == null)
            {
                errorResult = RedirectToAction("Index", new { packageId, message = "Package not found.", isSuccess = false });
                return false;
            }

            return true;
        }

        /// <summary>
        /// Common error handling pattern for sponsorship URL operations
        /// </summary>
        private ActionResult HandleSponsorshipUrlException(string packageId, Exception ex, string operation)
        {
            string message;
            if (ex is SponsorshipUrlValidationException validationEx)
            {
                message = validationEx.Message;
            }
            else
            {
                message = $"Error {operation} sponsorship URL: {ex.Message}";
            }

            return RedirectToAction("Index", new { packageId, message, isSuccess = false });
        }
    }
}
