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
    public class DeleteController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IPackageDeleteService _packageDeleteService;
        private readonly ITelemetryService _telemetryService;

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
            var packages = SearchForPackages(_packageService, query);
            var results = new List<PackageSearchResult>();
            foreach (var package in packages)
            {
                results.Add(CreatePackageSearchResult(package));
            }
            
            return Json(results, JsonRequestBehavior.AllowGet);
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
                    var parts = line.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);

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