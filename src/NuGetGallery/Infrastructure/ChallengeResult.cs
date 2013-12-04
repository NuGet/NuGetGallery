using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin.Security;

namespace NuGetGallery
{
    // Borrowed from ASP.Net template in VS 2013 :)
    public class ChallengeResult : HttpUnauthorizedResult
    {
        public ChallengeResult(string provider, string redirectUri)
        {
            LoginProvider = provider;
            RedirectUri = redirectUri;
        }

        public string LoginProvider { get; set; }
        public string RedirectUri { get; set; }
        
        public override void ExecuteResult(ControllerContext context)
        {
            var properties = new AuthenticationProperties() { RedirectUri = RedirectUri };
            context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
        }
    }
}