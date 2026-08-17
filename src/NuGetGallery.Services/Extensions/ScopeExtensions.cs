// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    /// <summary>
    /// APIs that provide lightweight extensibility for the Scope entity.
    /// </summary>
    public static class ScopeExtensions
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Determine if the scope allows any of the requested actions.
        /// </summary>
        /// <param name="scope">Credential scope.</param>
        /// <param name="requestedActions">Actions to validate.</param>
        /// <returns>True if any actions are allowed, false if none are.</returns>
        public static bool AllowsActions(this Scope scope, params string[] requestedActions)
        {
            if (requestedActions == null)
            {
                throw new ArgumentNullException(nameof(requestedActions));
            }

            return requestedActions.Any(action => AllowsAction(scope, action));
        }

        /// <summary>
        /// Determine if the scope allows the requested subject (package id).
        /// </summary>
        /// <param name="scope">Credential scope.</param>
        /// <param name="subject">Requested scope (package id) for comparison.</param>
        /// <returns>True if scope subjects match, false otehrwise.</returns>
        public static bool AllowsSubject(this Scope scope, string subject)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentNullException(nameof(subject));
            }

            return new Regex(
                    "^" + Regex.Escape(scope.Subject).Replace(@"\*", ".*") + "$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline,
                    RegexTimeout)
                .IsMatch(subject);
        }

        /// <summary>
        /// Determine if scope contains an owner scope.
        /// </summary>
        /// <param name="scope">Credential scope.</param>
        /// <returns>True if owner scope exists, false otherwise.</returns>
        public static bool HasOwnerScope(this Scope scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return scope.OwnerKey.HasValue;
        }

        public static User GetOwnerScope(this IEnumerable<Scope> scopes)
        {
            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            // Gallery currently restricts ApiKeys to a single owner scope.
            return scopes.Select(s => s.Owner)
                .Distinct()
                .SingleOrDefault();
        }

        public static bool HaveEqualScopesWithSameAllowedActionAndSubject(this IEnumerable<Scope> scopes1, IEnumerable<Scope> scopes2)
        {
            if (scopes1 == null && scopes2 == null)
            {
                return true;
            }

            if (scopes1 == null || scopes2 == null)
            {
                return false;
            }

            var s1 = scopes1.OrderBy(s => s.AllowedAction).ThenBy(s => s.Subject).ToList();
            var s2 = scopes2.OrderBy(s => s.AllowedAction).ThenBy(s => s.Subject).ToList();

            if (s1.Count != s2.Count)
            {
                return false;
            }

            for (int i = 0; i < s1.Count; i++)
            {
                if (!string.Equals(s1[i].AllowedAction, s2[i].AllowedAction, StringComparison.Ordinal) ||
                    !string.Equals(s1[i].Subject, s2[i].Subject, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AllowsAction(this Scope scope, string requestedAction)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return string.IsNullOrEmpty(requestedAction)
                || string.IsNullOrEmpty(scope.AllowedAction)
                || string.Equals(scope.AllowedAction, requestedAction, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope.AllowedAction, NuGetScopes.All, StringComparison.OrdinalIgnoreCase);
        }
    }
}
