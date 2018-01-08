// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Owin.Security.OpenIdConnect;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.CommonAuth
{
    public class CommonAuthAuthenticator : Authenticator<CommonAuthAuthenticatorConfiguration>
    {
        public static readonly string DefaultAuthenticationType = "CommonAuth";

        public const string Authority = "https://login.microsoftonline.com/{0}/v2.0";

        public const string V2CommonTenant = "common";

        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            // Fetch site root from configuration
            var siteRoot = config.Current.SiteRoot.TrimEnd('/') + "/";
            
            // We *always* require SSL for Authentication
            if (siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) 
            {
                siteRoot = siteRoot.Replace("http://", "https://");
            }

            // Configure OpenIdConnect
            var options = new OpenIdConnectAuthenticationOptions(BaseConfig.AuthenticationType)
            {
                RedirectUri = siteRoot + "users/account/authenticate/return",
                PostLogoutRedirectUri = siteRoot,
                Scope = OpenIdConnectScopes.OpenIdProfile + " email",
                ResponseType = OpenIdConnectResponseTypes.CodeIdToken,
                TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters() { ValidateIssuer = false },
            };

            Config.ApplyToOwinSecurityOptions(options);

            app.UseOpenIdConnectAuthentication(options);
        }
        
        public override AuthenticatorUI GetUI()
        {
            return new AuthenticatorUI(
                Strings.MicrosoftAccount_SignInMessage,
                Strings.MicrosoftAccount_SignInMessage,
                Strings.MicrosoftAccount_AccountNoun)
            {
                IconImagePath = "~/Content/gallery/img/microsoft-account.svg",
                IconImageFallbackPath = "~/Content/gallery/img/microsoft-account-24x24.png",
            };
        }

        public override ActionResult Challenge(string redirectUrl)
        {
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl);
        }

        public override bool IsAuthorForIdentity(ClaimsIdentity claimsIdentity)
        {
            Claim issuer = claimsIdentity.FindFirst(Constants.Claims.V2.Issuer);
            Claim tenant = claimsIdentity.FindFirst(Constants.Claims.V2.TenantId);
            if (issuer == null || tenant == null)
            {
                return false;
            }

            var expectedIssuer = string.Format(Authority, tenant.Value);
            return string.Equals(issuer.Value, expectedIssuer, StringComparison.OrdinalIgnoreCase);
        }

        public override bool TryMapIssuerToAuthenticationType(string issuer, out string authenticationType)
        {
            return base.TryMapIssuerToAuthenticationType(issuer, out authenticationType);
        }

        public override AuthInformation GetAuthInformation(ClaimsIdentity claimsIdentity)
        {
            return base.GetAuthInformation(claimsIdentity);
        }
    }
}