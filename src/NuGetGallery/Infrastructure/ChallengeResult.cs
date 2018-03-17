// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin.Security;

namespace NuGetGallery
{
    // Borrowed from ASP.Net template in VS 2013 :)
    public class ChallengeResult : HttpUnauthorizedResult
    {
        public ChallengeResult(string provider, string redirectUri, string mfaTokenValue = null)
        {
            LoginProvider = provider;
            RedirectUri = redirectUri;
            MfaTokenValue = mfaTokenValue;
        }

        public string LoginProvider { get; set; }
        public string RedirectUri { get; set; }
        public string MfaTokenValue { get; set; }

        public static string MFA_TOKEN_TYPE = "mfa_token";

        public override void ExecuteResult(ControllerContext context)
        {
            var dictionary = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(MfaTokenValue))
            {
                dictionary.Add(MFA_TOKEN_TYPE, MfaTokenValue.ToString());
            }

            var properties = new AuthenticationProperties(dictionary) { RedirectUri = RedirectUri };
            context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
        }
    }
}