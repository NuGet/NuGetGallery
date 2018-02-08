﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public static class ClaimsExtensions
    {
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
            return new IdentityInformation(identifierClaim.Value, nameClaim.Value, emailClaim?.Value, authType, tenantId: null);
        }

        public static bool HasDiscontinuedLoginCLaims(ClaimsIdentity identity)
        {
            if (identity == null || !identity.IsAuthenticated)
            {
                return false;
            }

            var discontinuedLoginClaim = identity.GetClaimOrDefault(NuGetClaims.DiscontinuedLogin);
            return !string.IsNullOrWhiteSpace(discontinuedLoginClaim)
                && NuGetClaims.DiscontinuedLoginValue.Equals(discontinuedLoginClaim, StringComparison.OrdinalIgnoreCase);
        }
    }
}
