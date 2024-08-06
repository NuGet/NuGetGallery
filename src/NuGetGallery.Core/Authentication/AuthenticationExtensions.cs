// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace NuGetGallery.Authentication
{
    public static class AuthenticationExtensions
    {
        public static string GetClaimOrDefault(this ClaimsIdentity self, string claimType)
        {
            return self.Claims.GetClaimOrDefault(claimType);
        }

        public static string GetClaimOrDefault(this IEnumerable<Claim> self, string claimType)
        {
            return self
                .Where(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .FirstOrDefault();
        }
    }
}
