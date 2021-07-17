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
            return View(new UpdateListedRequest());
        }

        [HttpGet]
        public virtual ActionResult Search(string query)
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
            if (ModelState.IsValid)
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
                    var packages = _packageService
                        .FindPackagesById(group.Key, PackageDeprecationFieldsToInclude.DeprecationAndRelationships)
                        .Where(x => normalizedVersions.Contains(x.NormalizedVersion))
                        .Where(x => x.Listed != updateListed.Listed)
                        .ToList();

                    packageRegistrationCount++;
                    packageCount += packages.Count;
                    noOpCount += normalizedVersions.Count - packages.Count;

                    await _packageUpdateService.UpdateListedInBulkAsync(packages, updateListed.Listed);
                }

                TempData["Message"] = $"{packageCount} packages across {packageRegistrationCount} package IDs have " +
                    $"been {(updateListed.Listed ? "relisted" : "unlisted")}. {noOpCount} packages were already " +
                    $"up-to-date and were left unchanged.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["Message"] = "The provided input is not valid.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}