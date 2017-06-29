// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Owin.Security.OpenIdConnect;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.AzureActiveDirectory
{
    public class AzureActiveDirectoryAuthenticator : Authenticator<AzureActiveDirectoryAuthenticatorConfiguration>
    {
        public static readonly string DefaultAuthenticationType = "AzureActiveDirectory";

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
                        if (notification.ProtocolMessage.RequestType == OpenIdConnectRequestType.LogoutRequest)
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
                Strings.AzureActiveDirectory_SignInMessage,
                Strings.AzureActiveDirectory_RegisterMessage,
                Strings.AzureActiveDirectory_AccountNoun)
            {
                ShowOnLoginPage = Config.ShowOnLoginPage
            };
        }

        public override ActionResult Challenge(string redirectUrl)
        {
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl);
        }

        public override bool TryMapIssuerToAuthenticationType(string issuer, out string authenticationType)
        {
            if (string.Equals(issuer, Config.Issuer, StringComparison.OrdinalIgnoreCase))
            {
                authenticationType = Config.AuthenticationType;
                return true;
            }

            return base.TryMapIssuerToAuthenticationType(issuer, out authenticationType);
        }
    }
}