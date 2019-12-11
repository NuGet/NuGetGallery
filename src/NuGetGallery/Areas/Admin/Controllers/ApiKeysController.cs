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
using NuGet.Services.Messaging.Email;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysController : AdminControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ITelemetryService _telemetryService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IMessageService _messageService;
        private readonly IMessageServiceConfiguration _messageServiceConfiguration;

        public ApiKeysController(IAuthenticationService authenticationService,
            ITelemetryService telemetryService,
            IEntitiesContext entitiesContext,
            IMessageService messageService,
            IMessageServiceConfiguration messageServiceConfiguration)
        {
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _messageServiceConfiguration = messageServiceConfiguration ?? throw new ArgumentNullException(nameof(messageServiceConfiguration));
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

            var queries = verifyQuery.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(q => q.Trim()).ToList();

            var results = new List<ApiKeyRevokeViewModel>();
            var verifiedApiKey = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var query in queries)
            {
                try
                {
                    var leakedApiKeyInfo = JsonConvert.DeserializeObject<LeakedApiKeyInfo>(query);

                    var apiKey = leakedApiKeyInfo.ApiKey;
                    if (!verifiedApiKey.Add(apiKey))
                    {
                        continue;
                    }
                    var leakedUrl = leakedApiKeyInfo.LeakedUrl;
                    var revocationSource = leakedApiKeyInfo.RevocationSource;

                    var credential = _authenticationService.GetApiKeyCredential(apiKey);
                    if (credential == null)
                    {
                        results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel: null, apiKey: apiKey, leakedUrl: null, revocationSource: null, isRevocable: false));
                        continue;
                    }

                    var apiKeyViewModel = new ApiKeyViewModel(_authenticationService.DescribeCredential(credential));
                    if (!_authenticationService.IsActiveApiKeyCredential(credential))
                    {
                        results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel, apiKey, leakedUrl: null, revocationSource: apiKeyViewModel.RevocationSource, isRevocable: false));
                        continue;
                    }
                    if (!Enum.TryParse(revocationSource, out CredentialRevocationSource revocationSourceKey))
                    {
                        return Json(HttpStatusCode.BadRequest, $"Invalid input! {query} is not using the supported Revocation Source: " +
                            $"{string.Join(",", Enum.GetNames(typeof(CredentialRevocationSource)))}.");
                    }

                    results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel, apiKey, leakedUrl, revocationSource, isRevocable: true));
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
                TempData["ErrorMessage"] = "The API keys revoking request can not be null.";
                return View(nameof(Index));
            }
            if (revokeApiKeysRequest.SelectedApiKeys == null || revokeApiKeysRequest.SelectedApiKeys.Count == 0)
            {
                TempData["ErrorMessage"] = "The API keys revoking request contains null or empty selected API keys.";
                return View(nameof(Index));
            }

            try
            {
                foreach (var selectedApiKey in revokeApiKeysRequest.SelectedApiKeys)
                {
                    var apiKeyInfo = JsonConvert.DeserializeObject<ApiKeyRevokeViewModel>(selectedApiKey);

                    var apiKeyCredential = _authenticationService.GetApiKeyCredential(apiKeyInfo.ApiKey);
                    var revocationSourceKey = (CredentialRevocationSource)Enum.Parse(typeof(CredentialRevocationSource), apiKeyInfo.RevocationSource);

                    var credentialRevokedMessage = new CredentialRevokedMessage(
                        _messageServiceConfiguration,
                        credential: apiKeyCredential,
                        leakedUrl: apiKeyInfo.LeakedUrl,
                        revocationSource: apiKeyInfo.RevocationSource,
                        manageApiKeyUrl: Url.ManageMyApiKeys(relativeUrl: false),
                        contactUrl: Url.Contact(relativeUrl: false));
                    await _messageService.SendMessageAsync(credentialRevokedMessage);

                    await _authenticationService.RevokeApiKeyCredential(apiKeyCredential, revocationSourceKey, commitChanges: false);
                }

                await _entitiesContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _telemetryService.TraceException(e);
                TempData["ErrorMessage"] = "Failed to revoke the API keys, and please check the telemetry for details.";
                return RedirectToAction("Index");
            }

            TempData["Message"] = "Successfully revoke the selected API keys.";
            return RedirectToAction("Index");
        }

        private class LeakedApiKeyInfo
        {
            [JsonProperty("ApiKey", Required = Required.Always)]
            public string ApiKey { get; set; }

            [JsonProperty("LeakedUrl", Required = Required.Always)]
            public string LeakedUrl { get; set; }

            [JsonProperty("RevocationSource", Required = Required.Always)]
            public string RevocationSource { get; set; }
        }
    }
}