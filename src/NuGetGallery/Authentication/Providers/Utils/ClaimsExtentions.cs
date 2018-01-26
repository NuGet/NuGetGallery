// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;

namespace NuGetGallery.Authentication.Providers.Utils
{
    public static class ClaimsExtentions
    {
        public static IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity, string authType)
        {
            var identifierClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            if (identifierClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {ClaimTypes.NameIdentifier}");
            }

            var nameClaim = claimsIdentity.FindFirst(ClaimTypes.Name);
            if (nameClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {ClaimTypes.Name}");
            }

            var emailClaim = claimsIdentity.FindFirst(ClaimTypes.Email);
            return new IdentityInformation(identifierClaim.Value, nameClaim.Value, emailClaim?.Value, authType, tenantId: null);
        }
    }
}
