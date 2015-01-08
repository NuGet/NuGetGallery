using System;
using System.Collections.Generic;
using System.IdentityModel;
using System.Linq;
using System.Web;
using Microsoft.Owin.Security.WsFederation;
using Owin;
using Microsoft.Owin.Security.Cookies;
using Microsoft.IdentityModel.Protocols;

namespace NuGetGallery.Authentication.Providers.ActiveDirectory
{
    public class ActiveDirectoryAuthenticator : Authenticator
    {
        protected override void AttachToOwinApp(Configuration.ConfigurationService config, Owin.IAppBuilder app)
        {
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = AuthenticationTypes.ActiveDirectory
            });

            app.UseWsFederationAuthentication(
                new WsFederationAuthenticationOptions {
                    MetadataAddress = "https://login.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/federationmetadata/2007-06/federationmetadata.xml",
                    Wtrealm = "http://nuget.org/ad-test",
                    AuthenticationType = AuthenticationTypes.ActiveDirectory
                });
        }

        public override AuthenticatorUI GetUI()
        {
            return new AuthenticatorUI(
                "Sign in with Azure AD",
                "Azure AD",
                "AAD")
            {
                IconCssClass = "icon-windows"
            };
        }
        protected internal override AuthenticatorConfiguration CreateConfigObject()
        {
            return new AuthenticatorConfiguration()
            {
                AuthenticationType = AuthenticationTypes.ActiveDirectory,
                Enabled = false
            };
        }

        public override System.Web.Mvc.ActionResult Challenge(string redirectUrl)
        {
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl);
        }
    }
}