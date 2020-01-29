// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Owin.Security.OpenIdConnect;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.AzureActiveDirectory
{
    public class AzureActiveDirectoryAuthenticator : Authenticator<AzureActiveDirectoryAuthenticatorConfiguration>
    {
        public static readonly string DefaultAuthenticationType = "AzureActiveDirectory";
        public static readonly string ClaimTypeName = "name";

        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            // Fetch site root from configuration
            var siteRoot = config.Current.SiteRoot.TrimEnd('/') + "/";

            // We *always* require SSL for Azure Active Directory
            if (siteRoot.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                siteRoot = siteRoot.Replace("http://", "https://");
            }

            // Configure OpenIdConnect
            var options = new OpenIdConnectAuthenticationOptions(BaseConfig.AuthenticationType)
            {
                RedirectUri = siteRoot + "users/account/authenticate/return",
                PostLogoutRedirectUri = siteRoot,
                Notifications = new OpenIdConnectAuthenticationNotifications
                {
                    RedirectToIdentityProvider = notification =>
                    {
                        if (notification.ProtocolMessage.RequestType == OpenIdConnectRequestType.Logout)
                        {
                            // We never intend to sign out at the federated identity. Suppress the redirect.
                            notification.HandleResponse();
                        }

                        return Task.FromResult(0);
                    }
                }
            };
            Config.ApplyToOwinSecurityOptions(options);

            app.UseOpenIdConnectAuthentication(options);
        }

        public override AuthenticatorUI GetUI()
        {
            return new AuthenticatorUI(
                ServicesStrings.AzureActiveDirectory_SignInMessage,
                ServicesStrings.AzureActiveDirectory_RegisterMessage,
                ServicesStrings.AzureActiveDirectory_AccountNoun)
            {
                ShowOnLoginPage = Config.ShowOnLoginPage
            };
        }

        public override bool IsProviderForIdentity(ClaimsIdentity claimsIdentity)
        {
            // If the issuer of the claims identity is same as that of the issuer for current authenticator then this is the author.
            var firstClaim = claimsIdentity?.Claims?.FirstOrDefault();
            if (firstClaim != null && string.Equals(firstClaim.Issuer, Config.Issuer, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return base.IsProviderForIdentity(claimsIdentity);
        }

        public override ActionResult Challenge(string redirectUrl, AuthenticationPolicy policy)
        {
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl, policy?.GetProperties());
        }

        public override IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity)
        {
            return ClaimsExtensions.GetIdentityInformation(
                claimsIdentity,
                DefaultAuthenticationType,
                ClaimTypes.NameIdentifier,
                ClaimTypeName,
                ClaimTypes.Name);
        }
    }
}