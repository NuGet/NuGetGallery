// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Filters;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class DeleteController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IPackageDeleteService _packageDeleteService;
        private readonly IUndoSoftDeleteBatchService _undoSoftDeleteBatchService;
        private readonly ITelemetryService _telemetryService;

        public DeleteController(
            IPackageService packageService,
            IPackageDeleteService packageDeleteService,
            IUndoSoftDeleteBatchService undoSoftDeleteBatchService,
            ITelemetryService telemetryService)
        {
            _packageService = packageService;
            _packageDeleteService = packageDeleteService;
            _undoSoftDeleteBatchService = undoSoftDeleteBatchService;
            _telemetryService = telemetryService;
        }

        [HttpGet]
        [ElevatedAdminAction]
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
        [ElevatedAdminAction]
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
        [ElevatedAdminAction]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> PreviewUndo(List<UndoPackageRequest> packages)
        {
            try
            {
                var selection = await _undoSoftDeleteBatchService.PreviewAsync(packages);
                return Json(new
                {
                    Success = true,
                    Packages = selection
                });
            }
            catch (Exception exception)
            {
                _telemetryService.TraceException(exception);
                return Json(new { Success = false, Message = exception.GetUserSafeMessage() });
            }
        }

        [HttpPost]
        [ElevatedAdminAction]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Undo(UndoPackagesRequest request)
        {
            if (request == null || !ModelState.IsValid)
            {
                return Json(new
                {
                    Success = false,
                    Message = ModelState.Values.SelectMany(state => state.Errors)
                        .Select(error => error.ErrorMessage)
                        .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message)) ?? "The undo request is invalid."
                });
            }

            try
            {
                var result = await _undoSoftDeleteBatchService.UndoAsync(
                    request.Packages, request.Reason, User.Identity.Name);
                var failures = result.Results.Where(entry => !entry.Success).ToList();
                foreach (var failure in failures)
                {
                    _telemetryService.TraceException(failure.Exception);
                }

                return Json(new
                {
                    result.Success,
                    Results = result.Results.Select(entry => new
                    {
                        entry.Package.Id,
                        entry.Package.Version,
                        entry.Success,
                        Message = entry.Success ? null : entry.Exception.GetUserSafeMessage()
                    }).ToList(),
                    Message = result.Success
                        ? "Successfully restored the selected packages. Feed and search updates may take time to appear."
                        : $"Completed undo for {result.RestoredCount} of {result.Results.Count} packages. Failed to restore: "
                            + string.Join(", ", failures.Select(failure => $"{failure.Package.Id}/{failure.Package.Version}"))
                });
            }
            catch (Exception exception)
            {
                _telemetryService.TraceException(exception);
                return Json(new { Success = false, Message = exception.GetUserSafeMessage() });
            }
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