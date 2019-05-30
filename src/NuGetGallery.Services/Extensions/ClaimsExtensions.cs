// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class ClaimsExtensions
    {
        private const string BooleanClaimDefault = "true";

        public static IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity, string authType)
        {
            return GetIdentityInformation(claimsIdentity, authType, ClaimTypes.NameIdentifier, ClaimTypes.Name, ClaimTypes.Email);
        }

        public static IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity, string authType, string nameIdentifierClaimType, string nameClaimType, string emailClaimType)
        {
            var identifierClaim = claimsIdentity.FindFirst(nameIdentifierClaimType);
            if (identifierClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {nameIdentifierClaimType}");
            }

            var nameClaim = claimsIdentity.FindFirst(nameClaimType);
            if (nameClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {nameClaimType}");
            }

            var emailClaim = claimsIdentity.FindFirst(emailClaimType);
            return new IdentityInformation(identifierClaim.Value, nameClaim.Value, emailClaim?.Value, authType, tenantId: null, usedMultiFactorAuth: false);
        }

        public static bool HasDiscontinuedLoginClaims(ClaimsIdentity identity)
        {
            if (identity == null || !identity.IsAuthenticated)
            {
                return false;
            }

            return HasBooleanClaim(identity, NuGetClaims.DiscontinuedLogin);
        }

        public static void AddBooleanClaim(List<Claim> claims, string claimType)
        {
            claims.Add(new Claim(claimType, BooleanClaimDefault));
        }

        public static bool HasBooleanClaim(ClaimsIdentity identity, string claimType)
        {
            return identity
                .GetClaimOrDefault(claimType)?
                .Equals(BooleanClaimDefault, StringComparison.OrdinalIgnoreCase)
                ?? false;
        }

        public static Claim CreateBooleanClaim(string claimType)
        {
            return new Claim(claimType, BooleanClaimDefault);
        }

        public static void AddExternalCredentialIdentityClaim(List<Claim> claims, string identityList)
        {
            claims.Add(new Claim(NuGetClaims.ExternalCredentialIdenities, identityList));
        }

        public static string GetExternalCredentialIdentityList(ClaimsIdentity identity)
        {
            if (identity == null)
            {
                return null;
            }

            return identity.GetClaimOrDefault(NuGetClaims.ExternalCredentialIdenities);
        }

        public static void AddExternalLoginCredentialTypeClaim(List<Claim> claims, string credentialType)
        {
            string claimValue = null;
            if (CredentialTypes.IsMicrosoftAccount(credentialType))
            {
                claimValue = NuGetClaims.ExternalLoginCredentialValues.MicrosoftAccount;
            }
            else if (CredentialTypes.IsAzureActiveDirectoryAccount(credentialType))
            {
                claimValue = NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory;
            }

            if (!string.IsNullOrEmpty(claimValue))
            {
                claims.Add(new Claim(NuGetClaims.ExternalLoginCredentialType, claimValue));
            }
        }

        public static bool LoggedInWithMicrosoftAccount(ClaimsIdentity identity)
        {
            return identity
                .GetClaimOrDefault(NuGetClaims.ExternalLoginCredentialType)?
                .Equals(NuGetClaims.ExternalLoginCredentialValues.MicrosoftAccount, StringComparison.OrdinalIgnoreCase)
                ?? false;
        }

        public static bool LoggedInWithAzureActiveDirectory(ClaimsIdentity identity)
        {
            return identity
                .GetClaimOrDefault(NuGetClaims.ExternalLoginCredentialType)?
                .Equals(NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory, StringComparison.OrdinalIgnoreCase)
                ?? false;
        }
    }
}
