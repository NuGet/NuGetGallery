// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysController : AdminControllerBase
    {
        private readonly IAuthenticationService _authenticationService;

        public ApiKeysController(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async virtual Task<ActionResult> Verify(string verifyQuery)
        {
            var queries = verifyQuery.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var results = new List<ApiKeyRevokeViewModel>();
            var verifiedApiKey = new HashSet<string>();
            foreach (var query in queries)
            {
                var queryObject = JsonConvert.DeserializeObject<ApiKeyAndLeakedURL>(query);
                var apiKey = queryObject.ApiKey;
                if (!verifiedApiKey.Add(apiKey))
                {
                    continue;
                }
                var leakedURL = queryObject.LeakedURL;

                var authenticationResult = await _authenticationService.Authenticate(apiKey);
                if (authenticationResult != null)
                {
                    var credential = authenticationResult.CredentialUsed;
                    var apiKeyViewModel = new ApiKeyViewModel(_authenticationService.DescribeCredential(credential));

                    results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel: apiKeyViewModel, apiKey: apiKey, leakedURL: leakedURL));
                }
                else
                {
                    results.Add(new ApiKeyRevokeViewModel(apiKeyViewModel: null, apiKey: apiKey, leakedURL: leakedURL));
                }
            }

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        private class ApiKeyAndLeakedURL
        {
            [JsonProperty("ApiKey")]
            public string ApiKey { get; set; }

            [JsonProperty("LeakedURL")]
            public string LeakedURL { get; set; }
        }
    }
}