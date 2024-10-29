// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class CorrectIsLatestController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageFileService _packageFileService;
        private readonly ITelemetryService _telemetryService;

        public CorrectIsLatestController(IPackageService packageService, IEntitiesContext entitiesContext, IPackageFileService packageFileService, ITelemetryService telemetryService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult CorrectIsLatestPackages()
        {
            var result = _entitiesContext
                .PackageRegistrations
                .Where(pr => pr.Packages.Any(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
                .Select(pr => new CorrectIsLatestPackage()
                {
                    Id = pr.Id,
                    Version = pr.Packages
                        .FirstOrDefault(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2)
                        .Version,
                    IsLatestCount = pr.Packages.Count(p => p.IsLatest),
                    IsLatestStableCount = pr.Packages.Count(p => p.IsLatestStable),
                    IsLatestSemVer2Count = pr.Packages.Count(p => p.IsLatestSemVer2),
                    IsLatestStableSemVer2Count = pr.Packages.Count(p => p.IsLatestStableSemVer2),
                    HasIsLatestUnlisted = pr.Packages.Any(p =>
                        !p.Listed
                        && (p.IsLatest
                        || p.IsLatestStable
                        || p.IsLatestSemVer2
                        || p.IsLatestStableSemVer2))
                })
                .Where(pr => pr.IsLatestCount > 1
                    || pr.IsLatestStableCount > 1
                    || pr.IsLatestSemVer2Count > 1
                    || pr.IsLatestStableSemVer2Count > 1
                    || pr.HasIsLatestUnlisted)
                .OrderBy(pr => pr.Id)
                .ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ReflowPackages(CorrectIsLatestRequest request)
        {
            if (request == null || request.Packages == null || request.Packages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, "Packages cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            var reflowPackageService = new ReflowPackageService(
                _entitiesContext,
                (PackageService)_packageService,
                _packageFileService,
                _telemetryService);

            var totalPackagesReflowed = 0;
            var totalPackagesFailReflowed = 0;

            foreach (var package in request.Packages)
            {
                try
                {
                    await reflowPackageService.ReflowAsync(package.Id, package.Version);
                    totalPackagesReflowed++;
                }
                catch (Exception ex)
                {
                    ex.Log();
                    totalPackagesFailReflowed++;
                }
            }

            var reflowedPackagesMessage = totalPackagesReflowed == 1 ? $"{totalPackagesReflowed} package reflowed" : $"{totalPackagesReflowed} packages reflowed";
            var failedPackagesMessage = totalPackagesFailReflowed == 1 ? $"{totalPackagesFailReflowed} package fail reflow" : $"{totalPackagesFailReflowed} packages fail reflow";

            return Json(HttpStatusCode.OK, $"{reflowedPackagesMessage}, {failedPackagesMessage}.", JsonRequestBehavior.AllowGet);
        }
    }
}
