// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using Newtonsoft.Json;

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
			
			// Get sponsorship URLs using the service
			var sponsorshipEntries = _sponsorshipUrlService.GetSponsorshipUrlEntries(package.PackageRegistration);
			result.SponsorshipUrls = sponsorshipEntries?.Select(e => e.Url).ToList() ?? new List<string>();
			
			results.Add(result);
		}

		return Json(results, JsonRequestBehavior.AllowGet);
	}		[HttpPost]
		public async Task<ActionResult> UpdateSponsorshipUrls(string packageId, string[] urls)
		{
			try
			{
				var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
				if (packageRegistration == null)
				{
					return Json(new { success = false, message = "Package not found." });
				}

				// Clear existing URLs first
				var existingEntries = _sponsorshipUrlService.GetSponsorshipUrlEntries(packageRegistration);
				foreach (var entry in existingEntries)
				{
					await _sponsorshipUrlService.RemoveSponsorshipUrlAsync(packageRegistration, entry.Url);
				}

				// Add new URLs using service layer
				var validatedUrls = new List<string>();
				foreach (var url in urls ?? new string[0])
				{
					if (!string.IsNullOrWhiteSpace(url))
					{
						try
						{
							await _sponsorshipUrlService.AddSponsorshipUrlAsync(packageRegistration, url);
							validatedUrls.Add(url);
						}
						catch (ArgumentException ex)
						{
							return Json(new { 
								success = false, 
								message = $"Invalid URL '{url}': {ex.Message}" 
							});
						}
					}
				}

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
