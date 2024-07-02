// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class UpdateListedController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IPackageUpdateService _packageUpdateService;

        public UpdateListedController(
            IPackageService packageService,
            IPackageUpdateService packageUpdateService)
        {
            _packageService = packageService;
            _packageUpdateService = packageUpdateService;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Search(string query)
        {
            var packages = SearchForPackages(_packageService, query);
            var results = new List<PackageSearchResult>();
            foreach (var package in packages)
            {
                results.Add(CreatePackageSearchResult(package));
            }

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateListed(UpdateListedRequest updateListed)
        {
            var idToVersions = updateListed
                .Packages
                .Select(x => x.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(x => x.Length == 2)
                .GroupBy(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);

            var packageRegistrationCount = 0;
            var packageCount = 0;
            var noOpCount = 0;
            foreach (var group in idToVersions)
            {
                var normalizedVersions = group
                    .Select(x => NuGetVersionFormatter.Normalize(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                List<Package> packages;
                if (normalizedVersions.Count == 1)
                {
                    packages = new List<Package>();
                    var package = _packageService.FindPackageByIdAndVersionStrict(group.Key, normalizedVersions.First());
                    if (package != null)
                    {
                        packages.Add(package);
                    }
                }
                else
                {
                    // Include the deprecation information since it is used in the auditing event.
                    packages = _packageService.FindPackagesById(
                            group.Key,
                            PackageDeprecationFieldsToInclude.DeprecationAndRelationships)
                        .Where(x => normalizedVersions.Contains(x.NormalizedVersion))
                        .ToList();
                }

                packages = packages
                    .Where(x => x.PackageStatusKey != PackageStatus.Deleted)
                    .Where(x => x.PackageStatusKey != PackageStatus.FailedValidation)
                    .ToList();

                packageCount += packages.Count;
                noOpCount += normalizedVersions.Count - packages.Count;

                if (packages.Any())
                {
                    packageRegistrationCount++;
                    await _packageUpdateService.UpdateListedInBulkAsync(packages, updateListed.Listed);
                }
            }

            TempData["Message"] = $"{packageCount} packages across {packageRegistrationCount} package IDs have " +
                $"been {(updateListed.Listed ? "relisted" : "unlisted")}. {noOpCount} packages were skipped because " +
                "they are deleted or they failed validation.";

            return RedirectToAction(nameof(Index));
        }
    }
}