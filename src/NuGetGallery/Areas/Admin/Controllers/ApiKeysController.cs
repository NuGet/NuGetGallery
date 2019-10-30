// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web.Mvc;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGetGallery.Authentication;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysController : AdminControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ITelemetryService _telemetryService;

        public ApiKeysController(IAuthenticationService authenticationService, ITelemetryService telemetryService)
        {
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(authenticationService));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Verify(string verifyQuery)
        {
            if (string.IsNullOrWhiteSpace(verifyQuery))
            {
                return Json(HttpStatusCode.BadRequest, "Invalid empty input!");
            }

            var queries = verifyQuery.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var results = new List<ApiKeyRevokeViewModel>();
            var verifiedApiKey = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var query in queries)
            {
                try
                {
                    var queryObject = JsonConvert.DeserializeObject<ApiKeyAndLeakedUrl>(query);

                    var apiKey = queryObject.ApiKey;
                    if (!verifiedApiKey.Add(apiKey))
                    {
                        continue;
                    }
                    var leakedUrl = queryObject.LeakedUrl;

                    var credential = _authenticationService.Verify(apiKey);
                    var apiKeyViewModel = credential == null ? null : new ApiKeyViewModel(_authenticationService.DescribeCredential(credential));

                    results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel, apiKey, leakedUrl, isRevocable: IsRevocable(apiKeyViewModel)));
                }
                catch (Exception e)
                {
                    if (e is JsonException)
                    {
                        return Json(HttpStatusCode.BadRequest, $"Invalid input! {query} is not using the valid JSON format.");
                    }

                    _telemetryService.TraceException(e);
                    return Json(HttpStatusCode.InternalServerError);
                }
            }

            return Json(HttpStatusCode.OK, results);
        }

        private bool IsRevocable(ApiKeyViewModel apiKeyViewModel)
        {
            if (apiKeyViewModel == null)
            {
                return false;
            }
            if (apiKeyViewModel.HasExpired)
            {
                return false;
            }
            if (!CredentialTypes.IsApiKey(apiKeyViewModel.Type))
            {
                return false;
            }

            return true;
        }

        private class ApiKeyAndLeakedUrl
        {
            [JsonProperty("ApiKey")]
            public string ApiKey { get; set; }

            [JsonProperty("LeakedUrl")]
            public string LeakedUrl { get; set; }
        }
    }
}