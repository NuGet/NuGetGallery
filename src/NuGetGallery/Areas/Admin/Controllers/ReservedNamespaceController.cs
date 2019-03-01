// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private IReservedNamespaceService _reservedNamespaceService;

        protected ReservedNamespaceController() { }

        public ReservedNamespaceController(IReservedNamespaceService reservedNamespaceService)
        {
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
        }

        [HttpGet]
        [ActionName(ActionName.AdminReservedNamespaceIndex)]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [ActionName(ActionName.AdminReservedNamespaceSearchPrefix)]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName(ActionName.AdminReservedNamespaceAddNamespace)]
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
        [ActionName(ActionName.AdminReservedNamespaceRemoveNamespace)]
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
        [ActionName(ActionName.AdminReservedNamespaceAddOwner)]
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
        [ActionName(ActionName.AdminReservedNamespaceRemoveOwner)]
        public async Task<JsonResult> RemoveOwner(ReservedNamespace prefix, string owner)
        {
            try
            {
                await _reservedNamespaceService.DeleteOwnerFromReservedNamespaceAsync(prefix.Value, owner, commitAsTransaction:true);
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