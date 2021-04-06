// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class DeleteController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IPackageDeleteService _packageDeleteService;
        private readonly ITelemetryService _telemetryService;

        protected DeleteController() { }

        public DeleteController(
            IPackageService packageService,
            IPackageDeleteService packageDeleteService,
            ITelemetryService telemetryService)
        {
            _packageService = packageService;
            _packageDeleteService = packageDeleteService;
            _telemetryService = telemetryService;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new DeletePackagesRequest
            {
                ReasonChoices = ReportMyPackageReasons
            };
            return View(model);
        }

        private static readonly ReportPackageReason[] ReportMyPackageReasons = {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            // Search suports several options:
            //   1) Full package id (e.g. jQuery)
            //   2) Full package id + version (e.g. jQuery 1.9.0)
            //   3) Any of the above separated by comma
            // We are not using Lucene index here as we want to have the database values.

            var queryParts = query.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var packages = new List<Package>();
            var completedQueryParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var queryPart in queryParts)
            {
                // Don't make the same query twice.
                if (!completedQueryParts.Add(queryPart.Trim()))
                {
                    continue;
                }

                var splitQueryPart = queryPart.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (splitQueryPart.Length == 1)
                {
                    var resultingRegistration = _packageService.FindPackageRegistrationById(splitQueryPart[0].Trim());
                    if (resultingRegistration != null)
                    {
                        packages.AddRange(resultingRegistration
                            .Packages
                            .OrderBy(p => NuGetVersion.Parse(p.NormalizedVersion)));
                    }
                }
                else if (splitQueryPart.Length == 2)
                {
                    var resultingPackage = _packageService.FindPackageByIdAndVersionStrict(splitQueryPart[0].Trim(), splitQueryPart[1].Trim());
                    if (resultingPackage != null)
                    {
                        packages.Add(resultingPackage);
                    }
                }
            }

            // Filter out duplicate packages and create the view model.
            var uniquePackagesKeys = new HashSet<int>();
            var results = new List<DeleteSearchResult>();
            foreach (var package in packages)
            {
                if (!uniquePackagesKeys.Add(package.Key))
                {
                    continue;
                }

                results.Add(CreateDeleteSearchResult(package));
            }
            
            return Json(results, JsonRequestBehavior.AllowGet);
        }

        private DeleteSearchResult CreateDeleteSearchResult(Package package)
        {
            return new DeleteSearchResult
            {
                PackageId = package.Id,
                PackageVersionNormalized = !string.IsNullOrEmpty(package.NormalizedVersion)
                    ? package.NormalizedVersion 
                    : NuGetVersion.Parse(package.Version).ToNormalizedString(),
                DownloadCount = package.DownloadCount,
                Created = package.Created.ToNuGetShortDateString(),
                Listed = package.Listed,
                PackageStatus = package.PackageStatusKey.ToString(),
                Owners = package
                    .PackageRegistration
                    .Owners
                    .Select(u => u.Username)
                    .OrderBy(u => u)
                    .Select(username => new UserViewModel
                    {
                        Username = username,
                        ProfileUrl = Url.User(username),
                    })
                    .ToList()
            };
        }

        [HttpGet]
        public virtual ActionResult Reflow()
        {
            return View(nameof(Reflow));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ReflowBulk(HardDeleteReflowBulkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BulkList))
            {
                TempData["ErrorMessage"] = "Must specify a list of hard-deleted packages to reflow in bulk!";

                return View(nameof(Reflow));
            }

            var lines = request.BulkList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                var requests = new List<HardDeleteReflowRequest>();

                foreach (var line in lines)
                {
                    var parts = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length != 2)
                    {
                        throw new UserSafeException(
                            $"Couldn't parse the list of hard-deleted packages to reflow in bulk: could not split \"{line}\" into ID and version!");
                    }

                    requests.Add(new HardDeleteReflowRequest() { Id = parts[0], Version = parts[1] });
                }

                return View(nameof(Reflow), new HardDeleteReflowBulkRequestConfirmation() { Requests = requests });
            }
            catch (Exception e)
            {
                _telemetryService.TraceException(e);

                TempData["ErrorMessage"] = e.GetUserSafeMessage();

                return View(nameof(Reflow));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ReflowBulkConfirm(HardDeleteReflowBulkRequestConfirmation confirmation)
        {
            var failures = new List<string>();

            foreach (var request in confirmation.Requests)
            {
                try
                {
                    await _packageDeleteService.ReflowHardDeletedPackageAsync(request.Id, request.Version);
                }
                catch (Exception e)
                {
                    failures.Add($"Failed to reflow hard-deleted package {request.Id} {request.Version}: {e.GetUserSafeMessage()}");
                }
            }

            if (failures.Any())
            {
                TempData["ErrorMessage"] = string.Join(" ", failures.ToArray());
            }
            else
            {
                TempData["Message"] =
                    "Successfully reflowed all packages.";
            }

            return View(nameof(Reflow));
        }
    }
}