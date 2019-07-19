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
        public ChallengeResult(string provider, string redirectUri, IDictionary<string, string> properties)
        {
            LoginProvider = provider;
            RedirectUri = redirectUri;
            Properties = properties ?? new Dictionary<string, string>();
        }

        public string LoginProvider { get; set; }
        public string RedirectUri { get; set; }
        public IDictionary<string, string> Properties { get; set; }

        public override void ExecuteResult(ControllerContext context)
        {
            var properties = new AuthenticationProperties(Properties) { RedirectUri = RedirectUri };
            context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
        }
    }
}