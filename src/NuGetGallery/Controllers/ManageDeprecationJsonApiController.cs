// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web.Mvc;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    public partial class ManageDeprecationJsonApiController
        : AppController
    {
        private readonly IVulnerabilityAutocompleteService _vulnerabilityAutocompleteService;
        private readonly IPackageService _packageService;

        public ManageDeprecationJsonApiController(
            IVulnerabilityAutocompleteService vulnerabilityAutocompleteService,
            IPackageService packageService)
        {
            _vulnerabilityAutocompleteService = vulnerabilityAutocompleteService ?? throw new ArgumentNullException(nameof(vulnerabilityAutocompleteService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        [HttpGet]
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
            var registration = _packageService.FindPackageRegistrationById(id);
            if (registration == null)
            {
                return Json(HttpStatusCode.NotFound, null, JsonRequestBehavior.AllowGet);
            }

            var versions = registration.Packages
                .Where(p => p.PackageStatusKey == PackageStatus.Available)
                .ToList()
                .OrderByDescending(p => NuGetVersion.Parse(p.Version))
                .Select(p => NuGetVersionFormatter.ToFullStringOrFallback(p.Version, p.Version));

            if (!versions.Any())
            {
                return Json(HttpStatusCode.NotFound, null, JsonRequestBehavior.AllowGet);
            }

            return Json(HttpStatusCode.OK, versions.ToList(), JsonRequestBehavior.AllowGet);
        }
    }
}