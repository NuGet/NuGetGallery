// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
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
            var error = await _deprecationManagementService.UpdateDeprecation(
                GetCurrentUser(),
                request.Id,
                request.Versions,
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

            return Json(HttpStatusCode.OK);
        }
    }
}