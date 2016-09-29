// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class ScopeEvaluator
    {
        public static bool HasScopeThatAllowsActionForSubject(
            string scopeClaim,
            string subject,
            string[] requestedActions)
        {
            if (string.IsNullOrEmpty(scopeClaim))
            {
                // Legacy API key, allow access...
                return true;
            }

            // Split scope claim
            var scopesFromClaim = scopeClaim.Split('|');
            foreach (var scopeFromClaim in scopesFromClaim)
            {
                var temp = scopeFromClaim.Split(new[] { ';' }, 2);

                var subjectMatches = string.IsNullOrEmpty(temp[0])
                                     || string.IsNullOrEmpty(subject)
                                     || temp[0] == subject;

                var actionMatches = requestedActions.Any(
                    allowed => string.IsNullOrEmpty(allowed)
                               || string.IsNullOrEmpty(temp[1])
                               || temp[1] == allowed
                               || temp[1] == NuGetScopes.All);

                if (subjectMatches && actionMatches)
                {
                    return true;
                }
            }

            return false;
        }
    }
}