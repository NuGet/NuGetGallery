// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace NuGetGallery.Authentication
{
    public static class SharedCookieConstants
    {
        public const string CookieName = ".AspNet.LocalUser";
        public const string AuthenticationScheme = "LocalUser";
        public const string DataProtectionApplicationName = "NuGetGallery";
        public const string DataProtectionMiddlewarePurpose = "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware";
        public const string DataProtectionFormatPurpose = "v2";
        public const string CookiePath = "/";

        public const string NameClaimType = ClaimTypes.Name;
        public const string NameIdentifierClaimType = ClaimTypes.NameIdentifier;
        public const string RoleClaimType = ClaimTypes.Role;
        public const string AuthenticationMethodClaimType = ClaimTypes.AuthenticationMethod;
        public const string DiscontinuedLoginClaimType = NuGetClaims.DiscontinuedLogin;
        public const string PasswordLoginClaimType = NuGetClaims.PasswordLogin;
        public const string ExternalLoginClaimType = NuGetClaims.ExternalLogin;
        public const string ExternalCredentialIdentitiesClaimType = NuGetClaims.ExternalCredentialIdenities;
        public const string EnabledMultiFactorAuthenticationClaimType = NuGetClaims.EnabledMultiFactorAuthentication;
        public const string WasMultiFactorAuthenticatedClaimType = NuGetClaims.WasMultiFactorAuthenticated;
        public const string ExternalLoginCredentialTypeClaimType = NuGetClaims.ExternalLoginCredentialType;

        public static readonly TimeSpan Expiration = TimeSpan.FromHours(6);
        public const bool SlidingExpiration = true;

        public static readonly IReadOnlyList<string> DataProtectionPurposes = Array.AsReadOnly(new[]
        {
            DataProtectionMiddlewarePurpose,
            AuthenticationScheme,
            DataProtectionFormatPurpose,
        });
    }
}
