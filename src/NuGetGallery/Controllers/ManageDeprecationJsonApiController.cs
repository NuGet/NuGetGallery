// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Auditing;
using NuGetGallery.Filters;
using NuGetGallery.RequestModels;

namespace NuGetGallery
{
    public partial class ManageDeprecationJsonApiController
        : AppController
    {
        private readonly IPackageDeprecationManagementService _deprecationManagementService;

        public ManageDeprecationJsonApiController(
            IPackageDeprecationManagementService deprecationManagementService)
        {
            _deprecationManagementService = deprecationManagementService ?? throw new ArgumentNullException(nameof(deprecationManagementService));
        }

        [HttpGet]
        [UIAuthorize]
        public virtual JsonResult GetAlternatePackageVersions(string id)
        {
            var versions = _deprecationManagementService.GetPossibleAlternatePackageVersions(id);
            return Json(HttpStatusCode.OK, versions, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [UIAuthorize]
        [RequiresAccountConfirmation("deprecate a package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> Deprecate(
            DeprecatePackageRequest request)
        {
            var isDeprecated = request.IsLegacy || request.HasCriticalBugs || request.IsOther;

            var error = await _deprecationManagementService.UpdateDeprecation(
                GetCurrentUser(),
                request.Id,
                request.Versions.ToList(),
                isDeprecated ? PackageDeprecatedVia.Web : PackageUndeprecatedVia.Web,
                request.IsLegacy,
                request.HasCriticalBugs,
                request.IsOther,
                request.AlternatePackageId,
                request.AlternatePackageVersion,
                request.CustomMessage);

            if (error != null)
            {
                return Json(error.Status, new { error = error.Message });
            }

            var packagePluralString = request.Versions.Count() > 1 ? "packages have" : "package has";
            var deprecatedString = isDeprecated ? "deprecated" : "undeprecated";
            TempData["Message"] = 
                $"Your {packagePluralString} been {deprecatedString}. " +
                "It may take several hours for this change to propagate through our system.";

            return Json(HttpStatusCode.OK);
        }
    }
}