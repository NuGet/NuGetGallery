﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    public partial class ManageDeprecationJsonApiController
        : AppController
    {
        private readonly IVulnerabilityAutocompleteService _vulnerabilityAutocompleteService;
        private readonly IPackageService _packageService;
        private readonly IPackageDeprecationService _deprecationService;
        private readonly IFeatureFlagService _featureFlagService;

        public ManageDeprecationJsonApiController(
            IVulnerabilityAutocompleteService vulnerabilityAutocompleteService,
            IPackageService packageService,
            IPackageDeprecationService deprecationService,
            IFeatureFlagService featureFlagService)
        {
            _vulnerabilityAutocompleteService = vulnerabilityAutocompleteService ?? throw new ArgumentNullException(nameof(vulnerabilityAutocompleteService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _deprecationService = deprecationService ?? throw new ArgumentNullException(nameof(deprecationService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        [HttpGet]
        [UIAuthorize]
        [ActionName("CveIds")]
        public JsonResult GetCveIds(string query)
        {
            // Get CVE data.
            // Suggestions will be CVE Id's that start with characters entered by the user.
            var queryResult = _vulnerabilityAutocompleteService.AutocompleteCveIds(query);
            var httpStatusCode = queryResult.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;

            return Json(
                httpStatusCode,
                queryResult,
                JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [UIAuthorize]
        [ActionName("CweIds")]
        public JsonResult GetCweIds(string query)
        {
            // Get CWE data.
            // Suggestions will be CWE Id's that start with characters entered by the user,
            // or CWE Id's that have a Name containing the textual search term provided by the user.
            var queryResult = _vulnerabilityAutocompleteService.AutocompleteCweIds(query);
            var httpStatusCode = queryResult.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;

            return Json(
                httpStatusCode,
                queryResult,
                JsonRequestBehavior.AllowGet);
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
            bool isVulnerable,
            bool isLegacy,
            bool isOther,
            IEnumerable<string> cveIds,
            decimal? cvssRating,
            IEnumerable<string> cweIds,
            string alternatePackageId,
            string alternatePackageVersion,
            string customMessage,
            bool shouldUnlist)
        {
            var currentUser = GetCurrentUser();
            if (!_featureFlagService.IsManageDeprecationEnabled(GetCurrentUser()))
            {
                return DeprecateErrorResponse(HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
            }

            if (versions == null || !versions.Any())
            {
                return DeprecateErrorResponse(HttpStatusCode.BadRequest, Strings.DeprecatePackage_NoVersions);
            }

            if (cvssRating.HasValue && (cvssRating < 0 || cvssRating > 10))
            {
                return DeprecateErrorResponse(HttpStatusCode.BadRequest, Strings.DeprecatePackage_InvalidCvss);
            }

            var packages = _packageService.FindPackagesById(id, withDeprecations: true);
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

            cveIds = cveIds ?? Enumerable.Empty<string>();
            var cves = _deprecationService.GetCvesById(cveIds);
            if (cveIds.Count() != cves.Count)
            {
                return DeprecateErrorResponse(HttpStatusCode.NotFound, Strings.DeprecatePackage_MissingCve);
            }

            cweIds = cweIds ?? Enumerable.Empty<string>();
            var cwes = _deprecationService.GetCwesById(cweIds);
            if (cweIds.Count() != cwes.Count)
            {
                return DeprecateErrorResponse(HttpStatusCode.NotFound, Strings.DeprecatePackage_MissingCwe);
            }

            var status = PackageDeprecationStatus.NotDeprecated;
            if (isVulnerable)
            {
                status |= PackageDeprecationStatus.Vulnerable;
            }

            if (isLegacy)
            {
                status |= PackageDeprecationStatus.Legacy;
            }

            if (isOther)
            {
                status |= PackageDeprecationStatus.Other;
            }

            await _deprecationService.UpdateDeprecation(
                packagesToUpdate,
                status,
                cves,
                cvssRating,
                cwes,
                alternatePackageRegistration,
                alternatePackage,
                customMessage,
                shouldUnlist);

            return Json(HttpStatusCode.OK);
        }

        private JsonResult DeprecateErrorResponse(HttpStatusCode code, string error)
        {
            return Json(code, new { error });
        }
    }
}