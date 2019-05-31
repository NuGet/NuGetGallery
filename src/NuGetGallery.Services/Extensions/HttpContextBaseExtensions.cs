// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Text;
using System.Web;
using System.Web.Security;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class HttpContextBaseExtensions
    {
        public static User GetCurrentUser(this HttpContextBase httpContext)
        {
            return httpContext.GetOwinContext().GetCurrentUser();
        }

        public static void SetConfirmationReturnUrl(this HttpContextBase httpContext, string returnUrl)
        {
            var confirmationContext = new ConfirmationContext
            {
                ReturnUrl = returnUrl,
            };
            string json = JsonConvert.SerializeObject(confirmationContext);
            string protectedJson = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(json), "ConfirmationContext"));
            HttpCookie responseCookie = new HttpCookie("ConfirmationContext", protectedJson);
            responseCookie.HttpOnly = true;
            httpContext.Response.Cookies.Add(responseCookie);
        }

        public static string GetConfirmationReturnUrl(this HttpContextBase httpContext)
        {
            HttpCookie cookie = null;
            if (httpContext.Request.Cookies != null)
            {
                cookie = httpContext.Request.Cookies.Get("ConfirmationContext");
            }

            if (cookie == null)
            {
                return null;
            }

            var protectedJson = cookie.Value;
            if (String.IsNullOrEmpty(protectedJson))
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(protectedJson), "ConfirmationContext"));
            var confirmationContext = JsonConvert.DeserializeObject<ConfirmationContext>(json);
            return confirmationContext.ReturnUrl;
        }

        /// <summary>
        /// Best effort attempt to extract client information from the user-agent header.
        /// According to documentation here: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/User-Agent 
        /// the common structure for user-agent header is:
        /// User-Agent: Mozilla/<version> (<system-information>) <platform> (<platform-details>) <extensions>
        /// Thus, extracting the part before the first '(', should give us product and version tokens in MOST cases.
        /// </summary>
        public static string GetClientInformation(this HttpContextBase httpContext)
        {
            string userAgent = httpContext.Request.Headers[ServicesConstants.UserAgentHeaderName];
            string result = string.Empty;

            if (!string.IsNullOrEmpty(userAgent))
            {
                int commentPartStartIndex = userAgent.IndexOf('(');

                if (commentPartStartIndex != -1)
                {
                    result = userAgent.Substring(0, commentPartStartIndex);
                }
                else
                {
                    result = userAgent;
                }

                result = result.Trim();
            }

            return result;
        }
    }

    public class ConfirmationContext
    {
        public string ReturnUrl { get; set; }
    }
}