// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class UpdateListedController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IUpdateListedService _updateListedService;

        public UpdateListedController(
            IPackageService packageService,
            IUpdateListedService updateListedService)
        {
            _packageService = packageService;
            _updateListedService = updateListedService;
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
            var packageIdentities = updateListed
                .Packages
                .Select(x => x.Split(['|'], StringSplitOptions.RemoveEmptyEntries))
                .Where(x => x.Length == 2)
                .Select(x => new UpdateListedPackageIdentity
                {
                    Id = x[0],
                    Version = x[1]
                })
                .ToList();

            var serviceResults = await _updateListedService.UpdateListedAsync(
                packageIdentities,
                updateListed.Listed);

            var packageCount = serviceResults.Count(r => r.Result == UpdateListedServiceResult.Success);
            var noOpCount = serviceResults.Count(r => r.Result == UpdateListedServiceResult.PackageNotFound);
            var registrationCount = serviceResults
                .Where(r => r.Result == UpdateListedServiceResult.Success)
                .Select(r => r.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            TempData["Message"] = $"{packageCount} packages across {registrationCount} package IDs have " +
                $"been {(updateListed.Listed ? "relisted" : "unlisted")}. {noOpCount} packages were skipped because " +
                "they are deleted or they failed validation.";

            return RedirectToAction(nameof(Index));
        }
    }
}
