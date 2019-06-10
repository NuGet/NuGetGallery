// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    public partial class ManageDeprecationJsonApiController
        : AppController
    {
        private readonly IPackageService _packageService;
        private readonly IPackageDeprecationService _deprecationService;
        private readonly IFeatureFlagService _featureFlagService;

        public ManageDeprecationJsonApiController(
            IPackageService packageService,
            IPackageDeprecationService deprecationService,
            IFeatureFlagService featureFlagService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _deprecationService = deprecationService ?? throw new ArgumentNullException(nameof(deprecationService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        [HttpGet]
        [UIAuthorize]
        public virtual JsonResult GetAlternatePackageVersions(string id)
        {
            var versions = _packageService.FindPackagesById(id)
                .Where(p => p.PackageStatusKey == PackageStatus.Available)
                .Select(p => NuGetVersion.Parse(p.Version))
                .OrderByDescending(v => v)
                .Select(v => v.ToFullString())
                .ToList();

            return Json(HttpStatusCode.OK, versions, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [UIAuthorize]
        [RequiresAccountConfirmation("deprecate a package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> Deprecate(
            string id,
            IEnumerable<string> versions,
            bool isLegacy,
            bool hasCriticalBugs,
            bool isOther,
            string alternatePackageId,
            string alternatePackageVersion,
            string customMessage)
        {
            var status = PackageDeprecationStatus.NotDeprecated;

            if (isLegacy)
            {
                status |= PackageDeprecationStatus.Legacy;
            }

            if (hasCriticalBugs)
            {
                status |= PackageDeprecationStatus.CriticalBugs;
            }

            if (isOther)
            {
                if (string.IsNullOrWhiteSpace(customMessage))
                {
                    return DeprecateErrorResponse(HttpStatusCode.BadRequest, Strings.DeprecatePackage_CustomMessageRequired);
                }

                status |= PackageDeprecationStatus.Other;
            }

            var currentUser = GetCurrentUser();
            if (!_featureFlagService.IsManageDeprecationEnabled(GetCurrentUser()))
            {
                return DeprecateErrorResponse(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (versions == null || !versions.Any())
            {
                return DeprecateErrorResponse(HttpStatusCode.BadRequest, Strings.DeprecatePackage_NoVersions);
            }

            var packages = _packageService.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships);
            var registration = packages.FirstOrDefault()?.PackageRegistration;
            if (registration == null)
            {
                // This should only happen if someone hacks the form or if the package is deleted while the user is filling out the form.
                return DeprecateErrorResponse(
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_MissingRegistration, id));
            }

            if (ActionsRequiringPermissions.DeprecatePackage.CheckPermissionsOnBehalfOfAnyAccount(currentUser, registration) != PermissionsCheckResult.Allowed)
            {
                return DeprecateErrorResponse(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (registration.IsLocked)
            {
                return DeprecateErrorResponse(
                    HttpStatusCode.Forbidden,
                    string.Format(Strings.DeprecatePackage_Locked, id));
            }

            PackageRegistration alternatePackageRegistration = null;
            Package alternatePackage = null;
            if (!string.IsNullOrWhiteSpace(alternatePackageId))
            {
                if (!string.IsNullOrWhiteSpace(alternatePackageVersion))
                {
                    alternatePackage = _packageService.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion);
                    if (alternatePackage == null)
                    {
                        return DeprecateErrorResponse(
                            HttpStatusCode.NotFound,
                            string.Format(Strings.DeprecatePackage_NoAlternatePackage, alternatePackageId, alternatePackageVersion));
                    }
                }
                else
                {
                    alternatePackageRegistration = _packageService.FindPackageRegistrationById(alternatePackageId);
                    if (alternatePackageRegistration == null)
                    {
                        return DeprecateErrorResponse(
                            HttpStatusCode.NotFound,
                            string.Format(Strings.DeprecatePackage_NoAlternatePackageRegistration, alternatePackageId));
                    }
                }
            }

            var packagesToUpdate = new List<Package>();
            foreach (var version in versions)
            {
                var normalizedVersion = NuGetVersionFormatter.Normalize(version);
                var package = packages.SingleOrDefault(v => v.NormalizedVersion == normalizedVersion);
                if (package == null)
                {
                    // This should only happen if someone hacks the form or if a version of the package is deleted while the user is filling out the form.
                    return DeprecateErrorResponse(
                        HttpStatusCode.NotFound,
                        string.Format(Strings.DeprecatePackage_MissingVersion, id));
                }
                else
                {
                    packagesToUpdate.Add(package);
                }
            }

            await _deprecationService.UpdateDeprecation(
                packagesToUpdate,
                status,
                alternatePackageRegistration,
                alternatePackage,
                customMessage,
                currentUser);

            return Json(HttpStatusCode.OK);
        }

        private JsonResult DeprecateErrorResponse(HttpStatusCode code, string error)
        {
            return Json(code, new { error });
        }
    }
}