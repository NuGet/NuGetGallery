// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Filters;
using NuGetGallery.RequestModels;

namespace NuGetGallery
{
    public partial class ManageDeprecationJsonApiController
        : AppController
    {
        private const int MaxCustomMessageLength = 4000;

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
            DeprecatePackageRequest request)
        {
            var status = PackageDeprecationStatus.NotDeprecated;

            if (request.IsLegacy)
            {
                status |= PackageDeprecationStatus.Legacy;
            }

            if (request.HasCriticalBugs)
            {
                status |= PackageDeprecationStatus.CriticalBugs;
            }

            if (request.IsOther)
            {
                if (string.IsNullOrWhiteSpace(request.CustomMessage))
                {
                    return DeprecateErrorResponse(HttpStatusCode.BadRequest, Strings.DeprecatePackage_CustomMessageRequired);
                }

                status |= PackageDeprecationStatus.Other;
            }

            string customMessage = null;
            if (request.CustomMessage != null)
            {
                if (request.CustomMessage.Length > MaxCustomMessageLength)
                {
                    return DeprecateErrorResponse(
                        HttpStatusCode.BadRequest,
                        string.Format(Strings.DeprecatePackage_CustomMessageTooLong, MaxCustomMessageLength));
                }

                customMessage = HttpUtility.HtmlEncode(request.CustomMessage);
            }

            if (request.Versions == null || !request.Versions.Any())
            {
                return DeprecateErrorResponse(HttpStatusCode.BadRequest, Strings.DeprecatePackage_NoVersions);
            }

            var packages = _packageService.FindPackagesById(request.Id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships);
            var registration = packages.FirstOrDefault()?.PackageRegistration;
            if (registration == null)
            {
                // This should only happen if someone hacks the form or if the package is deleted while the user is filling out the form.
                return DeprecateErrorResponse(
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_MissingRegistration, request.Id));
            }

            var currentUser = GetCurrentUser();
            if (!_featureFlagService.IsManageDeprecationEnabled(GetCurrentUser(), registration))
            {
                return DeprecateErrorResponse(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (ActionsRequiringPermissions.DeprecatePackage.CheckPermissionsOnBehalfOfAnyAccount(currentUser, registration) != PermissionsCheckResult.Allowed)
            {
                return DeprecateErrorResponse(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (registration.IsLocked)
            {
                return DeprecateErrorResponse(
                    HttpStatusCode.Forbidden,
                    string.Format(Strings.DeprecatePackage_Locked, request.Id));
            }

            PackageRegistration alternatePackageRegistration = null;
            Package alternatePackage = null;
            if (!string.IsNullOrWhiteSpace(request.AlternatePackageId))
            {
                if (!string.IsNullOrWhiteSpace(request.AlternatePackageVersion))
                {
                    alternatePackage = _packageService.FindPackageByIdAndVersionStrict(request.AlternatePackageId, request.AlternatePackageVersion);
                    if (alternatePackage == null)
                    {
                        return DeprecateErrorResponse(
                            HttpStatusCode.NotFound,
                            string.Format(Strings.DeprecatePackage_NoAlternatePackage, request.AlternatePackageId, request.AlternatePackageVersion));
                    }
                }
                else
                {
                    alternatePackageRegistration = _packageService.FindPackageRegistrationById(request.AlternatePackageId);
                    if (alternatePackageRegistration == null)
                    {
                        return DeprecateErrorResponse(
                            HttpStatusCode.NotFound,
                            string.Format(Strings.DeprecatePackage_NoAlternatePackageRegistration, request.AlternatePackageId));
                    }
                }
            }

            var packagesToUpdate = new List<Package>();
            foreach (var version in request.Versions)
            {
                var normalizedVersion = NuGetVersionFormatter.Normalize(version);
                var package = packages.SingleOrDefault(v => v.NormalizedVersion == normalizedVersion);
                if (package == null)
                {
                    // This should only happen if someone hacks the form or if a version of the package is deleted while the user is filling out the form.
                    return DeprecateErrorResponse(
                        HttpStatusCode.NotFound,
                        string.Format(Strings.DeprecatePackage_MissingVersion, request.Id));
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