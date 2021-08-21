// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class PackageOwnershipController : AdminControllerBase
    {
        private readonly IPackageService _packageService;

        public PackageOwnershipController(IPackageService packageService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        [HttpGet]
        public ViewResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ViewResult ValidateInput(PackageOwnershipChangesInput input)
        {
            // Find all package registrations
            var packageIds = input
                .PackageIds
                .Split(new[] { '\n' })
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x));

            var packageRegistrations = new List<PackageRegistration>();
            foreach (var packageId in packageIds)
            {
                var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
                if (packageRegistration == null)
                {
                    return ShowError(nameof(PackageOwnershipChangesInput.PackageIds), $"The package ID '{packageId}' does not exist.");
                }
                packageRegistrations.Add(packageRegistration);
            }

            if (packageRegistrations.Count == 0)
            {
                return ShowError(nameof(PackageOwnershipChangesInput.PackageIds), "You must provide at least one valid package ID.");
            }

            return View();
        }

        private ViewResult ShowError(string key, string message)
        {
            ModelState.AddModelError(key, message);
            return View(nameof(Index));
        }
    }
}