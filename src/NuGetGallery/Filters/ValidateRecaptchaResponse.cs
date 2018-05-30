// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace NuGetGallery.Filters
{
    public sealed class ValidateRecaptchaResponse : AuthorizeAttribute
    {
        private const string RecaptchaResponseId = "g-recaptcha-response";
        private const string RecaptchaValidationUrl = "https://www.google.com/recaptcha/api/siteverify?secret={0}&response={1}";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            var controller = filterContext.Controller as AppController;
            var privateKey = controller.NuGetContext.Config.Current.ReCaptchaPrivateKey;
            
            if (!string.IsNullOrEmpty(privateKey))
            {
                var response = controller.HttpContext.Request.Form[RecaptchaResponseId];
                if (!string.IsNullOrEmpty(response))
                {
                    if (!(Task.Run(() => RecaptchaIsValid(privateKey, response)).Result))
                    {
                        controller.TempData["Message"] = Strings.InvalidRecaptchaResponse;
                        controller.SafeRedirect(controller.Url.Home());
                    }
                }
            }

            base.OnAuthorization(filterContext);
        }

        private async Task<bool> RecaptchaIsValid(string privateKey, string response)
        {
            var validationUrl = string.Format(CultureInfo.InvariantCulture, RecaptchaValidationUrl, privateKey, response);
            using (var client = new HttpClient() { Timeout = RequestTimeout })
            {
                var reply = await client.GetStringAsync(validationUrl);
                var state = JsonConvert.DeserializeObject<RecaptchaState>(reply);
                return state.Success;
            }
        }

        public class RecaptchaState
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("error-codes")]
            public List<string> ErrorCodes { get; set; }
        }
    }
}