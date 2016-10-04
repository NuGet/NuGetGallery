// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class ScopeEvaluator
    {
        public static bool ScopeClaimsAllowsActionForSubject(
            string scopeClaim,
            string subject,
            string[] requestedActions)
        {
            if (string.IsNullOrEmpty(scopeClaim) || scopeClaim == "[]")
            {
                // Legacy API key, allow access...
                return true;
            }

            // Deserialize scope claim
            var scopesFromClaim = JsonConvert.DeserializeObject<List<Scope>>(scopeClaim);
            foreach (var scopeFromClaim in scopesFromClaim)
            {
                var subjectMatches = string.IsNullOrEmpty(scopeFromClaim.Subject)
                    || (!string.IsNullOrEmpty(subject) && scopeFromClaim.Subject == subject);

                var actionMatches = requestedActions.Any(
                    allowed => string.IsNullOrEmpty(allowed)
                               || string.IsNullOrEmpty(scopeFromClaim.AllowedAction)
                               || scopeFromClaim.AllowedAction == allowed
                               || scopeFromClaim.AllowedAction == NuGetScopes.All);

                if (subjectMatches && actionMatches)
                {
                    return true;
                }
            }

            return false;
        }
    }
}