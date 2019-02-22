// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class ManageDeprecationJsonApiController
        : AppController
    {
        private readonly IVulnerabilityAutocompleteService _vulnerabilityAutocompleteService;

        public ManageDeprecationJsonApiController(
            IVulnerabilityAutocompleteService vulnerabilityAutocompleteService)
        {
            _vulnerabilityAutocompleteService = vulnerabilityAutocompleteService ?? throw new ArgumentNullException(nameof(vulnerabilityAutocompleteService));
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
    }
}