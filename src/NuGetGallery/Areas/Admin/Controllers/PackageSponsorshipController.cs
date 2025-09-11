// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
	public class PackageSponsorshipController : AdminControllerBase
	{
		private readonly IPackageService _packageService;
		private readonly ISponsorshipLinksService _sponsorshipLinksService;

		public PackageSponsorshipController(
			IPackageService packageService,
			ISponsorshipLinksService sponsorshipLinksService)
		{
			_packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
			_sponsorshipLinksService = sponsorshipLinksService ?? throw new ArgumentNullException(nameof(sponsorshipLinksService));
		}

		[HttpGet]
		public ActionResult Index()
		{
			return View();
		}

	[HttpGet]
	public ActionResult Search(string query)
	{
		var packages = SearchForPackages(_packageService, query);
		
		// Group packages by PackageRegistration to get unique package IDs
		var packageRegistrations = packages
			.GroupBy(p => p.PackageRegistration)
			.Select(g => g.First()) // Take the first package from each registration
			.ToList();

		var results = new List<PackageSearchResult>();
		foreach (var package in packageRegistrations)
		{
			var result = CreatePackageSearchResult(package);
			
			// Get sponsorship URLs using the service at the package registration level
			var sponsorshipUrls = _sponsorshipLinksService.GetSponsorshipUrls(package.PackageRegistration);
			result.SponsorshipUrls = sponsorshipUrls?.ToList() ?? new List<string>();
			
			results.Add(result);
		}

		return Json(results, JsonRequestBehavior.AllowGet);
	}		[HttpPost]
		public ActionResult UpdateSponsorshipUrls(string packageId, string[] urls)
		{
			try
			{
				var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
				if (packageRegistration == null)
				{
					return Json(new { success = false, message = "Package not found." });
				}

				// Validate URLs
				var validatedUrls = new List<string>();
				foreach (var url in urls ?? new string[0])
				{
					if (!string.IsNullOrWhiteSpace(url))
					{
						string validatedUrl;
						string errorMessage;
						if (_sponsorshipLinksService.ValidateUrl(url, out validatedUrl, out errorMessage))
						{
							validatedUrls.Add(validatedUrl);
						}
						else
						{
							return Json(new { 
								success = false, 
								message = $"Invalid URL: {url}. {errorMessage}" 
							});
						}
					}
				}

				// Update sponsorship URLs
				_sponsorshipLinksService.UpdateSponsorshipUrls(packageRegistration, validatedUrls);

				return Json(new { 
					success = true, 
					message = "Sponsorship URLs updated successfully.",
					urls = validatedUrls
				});
			}
			catch (Exception ex)
			{
				return Json(new { 
					success = false, 
					message = $"Error updating sponsorship URLs: {ex.Message}" 
				});
			}
		}
	}
}
