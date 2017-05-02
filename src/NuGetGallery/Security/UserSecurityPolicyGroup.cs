// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;
using NuGetGallery.Authentication;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Grouping of one or more user security policies for enrollment.
    /// </summary>
    public class UserSecurityPolicyGroup
    {
        public const string SecurePush = "SecurePush";
        public const string SecurePushVersion = "4.1.0";

        /// <summary>
        /// Policy group name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Required policies.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies { get; set; }

        /// <summary>
        /// Action to take on user enrollment.
        /// </summary>
        public Action<User> OnEnroll { get; set; }

        /// <summary>
        /// Get supported user security policy groups.
        /// </summary>
        private static List<UserSecurityPolicyGroup> _instances;
        public static List<UserSecurityPolicyGroup> Instances
        {
            get
            {
                if (_instances == null)
                {
                    _instances = CreateUserPolicyGroups().ToList();
                }
                return _instances;
            }
        }
        
        private static IEnumerable<UserSecurityPolicyGroup> CreateUserPolicyGroups()
        {
            yield return new UserSecurityPolicyGroup()
            {
                Name = UserSecurityPolicyGroup.SecurePush,
                Policies = new[]
                {
                    new UserSecurityPolicy(RequirePackageVerifyScopePolicy.PolicyName),
                    RequireMinClientVersionForPushPolicy.CreatePolicy(new NuGetVersion(SecurePushVersion))
                },
                OnEnroll = OnEnroll_SecurePush
            };
        }

        private static void OnEnroll_SecurePush(User user)
        {
            var pushKeys = user.Credentials.Where(c =>
                c.Type.StartsWith(CredentialTypes.ApiKey.Prefix) &&
                (
                    c.Scopes.Count == 0 ||
                    c.Scopes.Any(s =>
                        s.AllowedAction.Equals(NuGetScopes.PackagePush, StringComparison.OrdinalIgnoreCase) ||
                        s.AllowedAction.Equals(NuGetScopes.PackagePushVersion, StringComparison.OrdinalIgnoreCase)
                        ))
                );
            
            foreach (var key in pushKeys)
            {
                var maxExpiration = DateTime.UtcNow.AddDays(7);
                if (!key.Expires.HasValue || key.Expires > maxExpiration)
                {
                    key.Expires = maxExpiration;
                }
            }
        }
    }
}