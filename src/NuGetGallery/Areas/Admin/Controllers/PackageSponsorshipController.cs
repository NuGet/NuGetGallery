// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
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
						// Use the service to get properly parsed sponsorship URLs
						model.SponsorshipUrls = _sponsorshipUrlService.GetSponsorshipUrlEntries(packageRegistration);
					}
				}
				else if (string.IsNullOrEmpty(message))
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
				var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
				if (packageRegistration == null)
				{
					return RedirectToAction("Index", new { packageId, message = "Package not found.", isSuccess = false });
				}

				var currentUser = GetCurrentUser();
				if (currentUser == null)
				{
					return RedirectToAction("Index", new { packageId, message = "User not authenticated.", isSuccess = false });
				}

				if (string.IsNullOrWhiteSpace(newSponsorshipUrl))
				{
					return RedirectToAction("Index", new { packageId, message = "Sponsorship URL cannot be empty.", isSuccess = false });
				}

				// Check server-side limit first
				var currentUrls = _sponsorshipUrlService.GetSponsorshipUrlEntries(packageRegistration);
				var maxLinks = _sponsorshipUrlService.TrustedSponsorshipDomains.MaxSponsorshipLinks;
				if (currentUrls.Count >= maxLinks)
				{
					return RedirectToAction("Index", new { packageId, message = $"You can add a maximum of {maxLinks} sponsorship links.", isSuccess = false });
				}

				var validatedUrl = await _sponsorshipUrlService.AddSponsorshipUrlAsync(packageRegistration, newSponsorshipUrl, currentUser);
				return RedirectToAction("Index", new { packageId, message = "Sponsorship URL added successfully.", isSuccess = true });
			}
			catch (ArgumentException ex)
			{
				return RedirectToAction("Index", new { packageId, message = $"Invalid URL: {ex.Message}", isSuccess = false });
			}
			catch (Exception ex)
			{
				return RedirectToAction("Index", new { packageId, message = $"Error adding sponsorship URL: {ex.Message}", isSuccess = false });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> RemoveUrl(string packageId, string sponsorshipUrl)
		{
			try
			{
				var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
				if (packageRegistration == null)
				{
					return RedirectToAction("Index", new { packageId, message = "Package not found.", isSuccess = false });
				}

				var currentUser = GetCurrentUser();
				if (currentUser == null)
				{
					return RedirectToAction("Index", new { packageId, message = "User not authenticated.", isSuccess = false });
				}

				await _sponsorshipUrlService.RemoveSponsorshipUrlAsync(packageRegistration, sponsorshipUrl, currentUser);
				return RedirectToAction("Index", new { packageId, message = "Sponsorship URL removed successfully.", isSuccess = true });
			}
			catch (Exception ex)
			{
				return RedirectToAction("Index", new { packageId, message = $"Error removing sponsorship URL: {ex.Message}", isSuccess = false });
			}
		}

	}
}
