// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NuGetGallery.Authentication
{
    public static class ScopeEvaluator
    {
        /// <summary>
        /// Evaluates if a scope claim allows at least one of the requested actions for a subject.
        /// </summary>
        /// <param name="scopeClaim">Json serialized array of <see cref="Scope"/></param>
        /// <param name="subject">The subject.</param>
        /// <param name="requestedActions">A list of requested actions <see cref="NuGetScopes"/></param>
        public static bool ScopeClaimsAllowsActionForSubject(
            string scopeClaim,
            string subject,
            string[] requestedActions)
        {
            if (IsEmptyScopeClaim(scopeClaim))
            {
                // Legacy API key, allow access...
                return true;
            }

            // Deserialize scope claim
            var scopesFromClaim = JsonConvert.DeserializeObject<List<Scope>>(scopeClaim);
            foreach (var scopeFromClaim in scopesFromClaim)
            {
                var subjectMatches = string.IsNullOrEmpty(subject) || (!string.IsNullOrEmpty(subject) && subject.MatchesPackagePattern(scopeFromClaim.Subject));

                if (subjectMatches && ActionAllows(scopeFromClaim.AllowedAction, requestedActions))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsEmptyScopeClaim(string scopeClaim)
        {
            return string.IsNullOrEmpty(scopeClaim) || scopeClaim == "[]";
        }

        public static bool AllowsPush(this Scope scope)
        {
            return ActionAllows(scope.AllowedAction, NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion);
        }

        private static bool ActionAllows(string allowedAction, params string[] requestedActions)
        {
            return requestedActions.Any(
                requestedAction => string.IsNullOrEmpty(requestedAction)
                           || string.IsNullOrEmpty(allowedAction)
                           || string.Equals(allowedAction, requestedAction, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(allowedAction, NuGetScopes.All, StringComparison.OrdinalIgnoreCase));
        }
    }
}