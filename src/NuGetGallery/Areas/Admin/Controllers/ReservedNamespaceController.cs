// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System.Threading.Tasks;
using NuGetGallery.Areas.Admin.ViewModels;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ReservedNamespaceController : AdminControllerBase
    {
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrations;

        protected ReservedNamespaceController() { }

        public ReservedNamespaceController(
            IReservedNamespaceService reservedNamespaceService,
            IEntityRepository<PackageRegistration> packageRegistrations)
        {
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _packageRegistrations = packageRegistrations ?? throw new ArgumentNullException(nameof(packageRegistrations));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View(new ReservedNamespaceViewModel());
        }

        [HttpGet]
        public JsonResult SearchPrefix(string query)
        {
            var prefixQueries = GetPrefixesFromQuery(query);

            var foundPrefixes = _reservedNamespaceService.FindReservedNamespacesForPrefixList(prefixQueries);

            var notFoundPrefixQueries = prefixQueries.Except(foundPrefixes.Select(p => p.Value), StringComparer.OrdinalIgnoreCase);
            var notFoundPrefixes = notFoundPrefixQueries.Select(q => new ReservedNamespace(value: q, isSharedNamespace: false, isPrefix: true));

            var resultModel = foundPrefixes.Select(fp => new ReservedNamespaceResultModel(fp, isExisting: true));
            resultModel = resultModel.Concat(notFoundPrefixes.Select(nfp => new ReservedNamespaceResultModel(nfp, isExisting: false)).ToList());

            var results = new ReservedNamespaceSearchResult
            {
                Prefixes = resultModel
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult FindNamespacesByPrefix(string prefix)
        {
            var namespaces = _reservedNamespaceService
                .FindAllReservedNamespacesForPrefix(prefix, getExactMatches: false)
                .OrderBy(x => x.Value)
                .ThenBy(x => x.IsPrefix)
                .ToList();
            var model = new ReservedNamespaceViewModel { ReservedNamespacesQuery = prefix, ReservedNamespaces = namespaces };
            return View(nameof(Index), model);
        }

        [HttpGet]
        public ActionResult FindPackagesByPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return RedirectToAction(nameof(Index));
            }

            var packageRegistrations = _packageRegistrations
                .GetAll()
                .Include(x => x.Owners)
                .Include(x => x.ReservedNamespaces)
                .Where(x => x.Id.StartsWith(prefix))
                .OrderBy(x => x.Id)
                .ToList();
            var model = new ReservedNamespaceViewModel { PackageRegistrationsQuery = prefix, PackageRegistrations = packageRegistrations };
            return View(nameof(Index), model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddNamespace(ReservedNamespace newNamespace)
        {
            try
            {
                await _reservedNamespaceService.AddReservedNamespaceAsync(newNamespace);
                return Json(new { success = true, message = string.Format(Strings.ReservedNamespace_PrefixAdded, newNamespace.Value) });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> RemoveNamespace(ReservedNamespace existingNamespace)
        {
            try
            {
                await _reservedNamespaceService.DeleteReservedNamespaceAsync(existingNamespace.Value);
                return Json(new { success = true, message = string.Format(Strings.ReservedNamespace_PrefixRemoved, existingNamespace.Value) });
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return Json(new { success = false, message = ex.Message });
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddOwner(ReservedNamespace prefix, string owner)
        {
            try
            {
                await _reservedNamespaceService.AddOwnerToReservedNamespaceAsync(prefix.Value, owner);
                return Json(new { success = true, message = string.Format(Strings.ReservedNamespace_OwnerAdded, owner, prefix.Value) });
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> RemoveOwner(ReservedNamespace prefix, string owner)
        {
            try
            {
                await _reservedNamespaceService.DeleteOwnerFromReservedNamespaceAsync(prefix.Value, owner, commitChanges: true);
                return Json(new { success = true, message = string.Format(Strings.ReservedNamespace_OwnerRemoved, owner, prefix.Value) });
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private static List<string> GetPrefixesFromQuery(string query)
        {
            query = query ?? "";
            return query.Split(',', '\r', '\n', ';')
                .Select(prefix => prefix.Trim())
                .Where(prefix => !string.IsNullOrEmpty(prefix))
                .ToList();
        }
    }
}