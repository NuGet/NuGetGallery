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
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class ValidateRecaptchaResponseAttribute : ActionFilterAttribute
    {
        private const string RecaptchaResponseId = "g-recaptcha-response";
        private const string RecaptchaValidationUrl = "https://www.google.com/recaptcha/api/siteverify?secret={0}&response={1}";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly Lazy<HttpClient> Client = new Lazy<HttpClient>(() => new HttpClient() { Timeout = RequestTimeout });

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = filterContext.Controller as AppController;
            var privateKey = controller.NuGetContext.Config.Current.ReCaptchaPrivateKey;
            
            if (!string.IsNullOrEmpty(privateKey))
            {
                var response = controller.HttpContext.Request.Form[RecaptchaResponseId];

                if (!(Task.Run(() => RecaptchaIsValid(privateKey, response)).Result))
                {
                    controller.TempData["Message"] = Strings.InvalidRecaptchaResponse;
                    filterContext.Result = new RedirectResult(controller.Url.Current());
                }
            }

            base.OnActionExecuting(filterContext);
        }

        private async Task<bool> RecaptchaIsValid(string privateKey, string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return false;
            }

            var validationUrl = string.Format(CultureInfo.InvariantCulture, RecaptchaValidationUrl, privateKey, response);

            try
            {
                var reply = await Client.Value.GetStringAsync(validationUrl);
                var state = JsonConvert.DeserializeObject<RecaptchaState>(reply);
                return state.Success;
            }
            catch (Exception)
            {
                return false;
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