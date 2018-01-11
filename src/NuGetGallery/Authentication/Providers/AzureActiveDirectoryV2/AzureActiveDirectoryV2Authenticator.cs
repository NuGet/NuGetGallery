// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using System.Web.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Owin.Security.OpenIdConnect;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.AzureActiveDirectoryV2
{
    public class AzureActiveDirectoryV2Authenticator : Authenticator<AzureActiveDirectoryV2AuthenticatorConfiguration>
    {
        public static class V2Claims
        {
            public const string TenantId = "http://schemas.microsoft.com/identity/claims/tenantid";
            public const string Identifier = "http://schemas.microsoft.com/identity/claims/objectidentifier";
            public const string Email = "preferred_username";
            public const string Name = "name";
            public const string Issuer = "iss";
        }

        public static class AuthenticationType
        {
            public const string MicrosoftAccount = "MicrosoftAccount";
            public const string AzureActiveDirectory = "AzureActiveDirectory";
        }

        public static readonly string DefaultAuthenticationType = "AzureActiveDirectoryV2";
        public static readonly string PersonalMSATenant = "9188040d-6c67-4c5b-b112-36a304b66dad";
        public static readonly string V2CommonTenant = "common";
        public static readonly string Authority = "https://login.microsoftonline.com/{0}/v2.0";

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

        public override bool IsProviderForIdentity(ClaimsIdentity claimsIdentity)
        {
            Claim issuer = claimsIdentity.FindFirst(V2Claims.Issuer);
            Claim tenant = claimsIdentity.FindFirst(V2Claims.TenantId);
            if (issuer == null || tenant == null)
            {
                return false;
            }

            var expectedIssuer = string.Format(Authority, tenant.Value);
            return string.Equals(issuer.Value, expectedIssuer, StringComparison.OrdinalIgnoreCase);
        }

        public override IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity)
        {
            if (!IsProviderForIdentity(claimsIdentity))
            {
                throw new ArgumentException($"The identity is not authored by {nameof(AzureActiveDirectoryV2Authenticator)}");
            }

            var tenantClaim = claimsIdentity.FindFirst(V2Claims.TenantId);
            if (tenantClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {V2Claims.TenantId}");
            }

            var tenantId = tenantClaim.Value;
            string authenticationType = null;
            string identifier = null;
            var idClaim = claimsIdentity.FindFirst(V2Claims.Identifier);
            if (idClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: '{V2Claims.Identifier}'");
            }

            if (string.Equals(tenantId, PersonalMSATenant, StringComparison.OrdinalIgnoreCase))
            {
                authenticationType = AuthenticationType.MicrosoftAccount;

                // The MSA v2 authentication identifier is returned as 32 character alphanumeric value(padded with 0 and -), 
                // where as the existing Microsoft account identifiers are 16 character wide.
                // For e.g old format: 0ae45d63e22e4a60, newer format: 00000000-0000-0000-0AE4-5D63-E22E4A60
                // We need to format the values into the older format for backwards compatibility
                identifier = idClaim.Value.Replace("-", "").Substring(16).ToLowerInvariant();
            }
            else
            {
                authenticationType = AuthenticationType.AzureActiveDirectory;
                identifier = idClaim.Value;
            }

            var nameClaim = claimsIdentity.FindFirst(V2Claims.Name);
            var emailClaim = claimsIdentity.FindFirst(V2Claims.Email);
            if (emailClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: '{V2Claims.Email}'");
            }

            return new IdentityInformation(identifier, nameClaim?.Value, emailClaim.Value, authenticationType, tenantId);
        }
    }
}