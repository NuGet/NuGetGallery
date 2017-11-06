// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    /// <summary>
    /// APIs that provide lightweight extensibility for .NET security principal types.
    /// </summary>
    public static class PrincipalExtensions
    {
        /// <summary>
        /// Get a security claim for the current user context.
        /// </summary>
        /// <param name="self">Current user principal.</param>
        /// <param name="claimType">Claim type</param>
        /// <returns>Value of the claim, or null if does not exist.</returns>
        public static string GetClaimOrDefault(this ClaimsPrincipal self, string claimType)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentNullException(nameof(claimType));
            }

            return self.Claims.GetClaimOrDefault(claimType);
        }

        /// <summary>
        /// Get the authentication type string.
        /// </summary>
        /// <param name="self">Current user principal identity.</param>
        /// <returns>Authentication type.</returns>
        public static string GetAuthenticationType(this IIdentity self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            var identity = self as ClaimsIdentity;
            return identity?.GetClaimOrDefault(ClaimTypes.AuthenticationMethod);
        }

        /// <summary>
        /// Get scope entities from the current user context's claims.
        /// </summary>
        /// <param name="self">Current user identity.</param>
        /// <returns>Scopes for current user, or null if none.</returns>
        public static List<Scope> GetScopesFromClaim(this IIdentity self)
        {
            var claim = GetScopeClaim(self);

            return string.IsNullOrWhiteSpace(claim)?
                null : JsonConvert.DeserializeObject<List<Scope>>(claim);
        }

        /// <summary>
        /// Determine if the current user context is a Gallery administrator.
        /// </summary>
        /// <param name="self">Current user principal.</param>
        /// <returns>True if Gallery administrator, false otherwise.</returns>
        public static bool IsAdministrator(this IPrincipal self)
        {
            if (self == null || self.Identity == null)
            {
                return false;
            }

            return self.Identity.IsAuthenticated && self.IsInRole(Constants.AdminRoleName);
        }

        /// <summary>
        /// Determine if the current user context is authenticated with a scoped API key.
        /// </summary>
        /// <param name="self">Current user identity.</param>
        /// <returns>True if authenticated with scoped API key, false otherwise.</returns>
        public static bool IsScopedAuthentication(this IIdentity self)
        {
            return !IsEmptyScopeClaim(GetScopeClaim(self));
        }

        /// <summary>
        /// Determine if the current user context has any of the requested actions.
        /// </summary>
        /// <param name="self">Current user identity.</param>
        /// <param name="requestedActions">Actions to validate.</param>
        /// <returns>True if has any of the actions, false if none.</returns>
        public static bool HasExplicitScopeAction(this IIdentity self, params string[] requestedActions)
        {
            // Scoped API key with matching actions.
            var scopes = GetScopesFromClaim(self);
            return scopes != null && scopes.Any(s => s.AllowsActions(requestedActions));
        }

        public static bool MatchesUser(this IPrincipal self, User user)
        {
            return self.Identity.Name == user.Username;
        }

        /// <summary>
        /// Determine if the current user context allows any of the requested actions.
        /// </summary>
        /// <param name="self">Current user identity.</param>
        /// <param name="requestedActions">Actions to validate.</param>
        /// <returns>True if any actions are allowed, false if none are.</returns>
        public static bool HasScopeThatAllowsActions(this IIdentity self, params string[] requestedActions)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            return !self.IsScopedAuthentication() || self.HasExplicitScopeAction(requestedActions);
        }
        
        private static string GetScopeClaim(IIdentity self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            var identity = self as ClaimsIdentity;
            return identity?.GetClaimOrDefault(NuGetClaims.Scope);
        }
        
        private static bool IsEmptyScopeClaim(string scopeClaim)
        {
            return string.IsNullOrEmpty(scopeClaim) || scopeClaim == "[]";
        }
    }
}