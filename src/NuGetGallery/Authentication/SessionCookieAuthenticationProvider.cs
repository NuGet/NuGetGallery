using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin.Security.Cookies;

namespace NuGetGallery.Authentication
{
    public class SessionCookieAuthenticationProvider : CookieAuthenticationProvider
    {
        public override void ResponseSignIn(CookieResponseSignInContext context)
        {
            base.ResponseSignIn(context);
        }

        public override Task ValidateIdentity(CookieValidateIdentityContext context)
        {
            return base.ValidateIdentity(context);
        }
    }
}