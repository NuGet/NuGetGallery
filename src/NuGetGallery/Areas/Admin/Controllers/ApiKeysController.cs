// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysController : AdminControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ITelemetryService _telemetryService;

        public ApiKeysController(IAuthenticationService authenticationService, ITelemetryService telemetryService)
        {
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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
                    var revokedBy = queryObject.RevokedBy;

                    var credential = _authenticationService.GetApiKeyCredential(apiKey);
                    var apiKeyViewModel = credential == null ? null : new ApiKeyViewModel(_authenticationService.DescribeCredential(credential));
                    if (apiKeyViewModel == null)
                    {
                        results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel, apiKey, leakedUrl: null, revokedBy: null, isRevocable: false));
                        continue;
                    }
                    if (!IsRevocable(apiKeyViewModel))
                    {
                        results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel, apiKey, leakedUrl: null, revokedBy: apiKeyViewModel.RevokedBy, isRevocable: false));
                        continue;
                    }

                    var supportedRevokedByType = Enum.GetNames(typeof(CredentialRevokedByType));
                    if (!supportedRevokedByType.Contains(revokedBy))
                    {
                        return Json(HttpStatusCode.BadRequest, $"Invalid input! {query} is not using the supported revokedBy types: " +
                            $"{string.Join(",", supportedRevokedByType.ToArray())}.");
                    }

                    results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel, apiKey, leakedUrl, revokedBy, isRevocable: true));
                }
                catch (JsonException)
                {
                    return Json(HttpStatusCode.BadRequest, $"Invalid input! {query} is not using the valid JSON format.");
                }
            }

            return Json(HttpStatusCode.OK, results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Revoke(RevokeApiKeysRequest revokeApiKeysRequest)
        {
            if (revokeApiKeysRequest == null)
            {
                TempData["ErrorMessage"] = "The API keys revoking request can not be null!";
                return View(nameof(Index));
            }

            var failures = new List<string>();
            foreach (var selectedApiKey in revokeApiKeysRequest.SelectedApiKeys)
            {
                var apiKeyInfo = JsonConvert.DeserializeObject<ApiKeyRevokeViewModel>(selectedApiKey);
                try
                {
                    var apiKeyCredential = _authenticationService.GetApiKeyCredential(apiKeyInfo.ApiKey);
                    var credentialRevokedByType = (CredentialRevokedByType) Enum.Parse(typeof(CredentialRevokedByType), apiKeyInfo.RevokedBy);
                    await _authenticationService.RevokeCredential(apiKeyCredential, credentialRevokedByType);
                }
                catch(Exception e)
                {
                    failures.Add($"Failed to revoke the API key {apiKeyInfo.ApiKey}: {e.GetUserSafeMessage()}");
                    _telemetryService.TraceException(e);
                }
            }

            if (failures.Any())
            {
                TempData["ErrorMessage"] = string.Join(" ", failures.ToArray());
            }
            else
            {
                TempData["Message"] = "Successfully revoke the selected API keys.";
            }

            return RedirectToAction("Index");
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
            if (apiKeyViewModel.RevokedBy != null)
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

            [JsonProperty("RevokedBy")]
            public string RevokedBy { get; set; }
        }
    }
}