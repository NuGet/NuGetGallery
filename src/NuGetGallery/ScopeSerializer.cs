// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class ScopeSerializer
    {
        /// <summary>
        /// Serialize a single scope into a string
        /// </summary>
        /// <param name="scope">Scope to serialize</param>
        /// <returns>Serialized scope, e.g. packageid;package:list</returns>
        public static string SerializeScope(Scope scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return string.Join("{0};{1}",
                scope.Subject ?? string.Empty,
                scope.AllowedAction ?? NuGetScopes.All);
        }

        /// <summary>
        /// Serialize scopes into a string
        /// </summary>
        /// <param name="scopes">Scopes to serialize</param>
        /// <returns>Serialized scopes, e.g. packageid;package:list|packageid2;package:push</returns>
        public static string SerializeScopes(IEnumerable<Scope> scopes)
        {
            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            return string.Join("|", scopes.Select(SerializeScope));
        }

        /// <summary>
        /// Deserializes a single scope from a string
        /// </summary>
        /// <param name="serializedScope">Serialized scope, e.g. packageid;package:list</param>
        /// <returns>Deserialized scope</returns>
        public static Scope DeserializeScope(string serializedScope)
        {
            if (serializedScope == null)
            {
                throw new ArgumentNullException(nameof(serializedScope));
            }

            var temp = serializedScope.Split(new[] { ';' }, 2);
            if (temp.Length == 2)
            {
                return new Scope(temp[0], temp[1]);
            }

            return new Scope(null, temp[0]);
        }

        /// <summary>
        /// Deserializes scopes from a string
        /// </summary>
        /// <param name="serializedScopes">Serialized scopes, e.g. packageid;package:list|packageid2;package:push</param>
        /// <returns>Deserialized scopes</returns>
        public static IEnumerable<Scope> DeserializeScopes(string serializedScopes)
        {
            if (serializedScopes == null)
            {
                throw new ArgumentNullException(nameof(serializedScopes));
            }

            var scopesFromClaim = serializedScopes.Split('|');
            foreach (var scopeFromClaim in scopesFromClaim)
            {
                yield return DeserializeScope(scopeFromClaim);
            }
        }
    }
}