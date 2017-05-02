// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Security
{
    public static class UserSecurityPolicyExtensions
    {
        /// <summary>
        /// Determine whether two security policies are equivalent.
        /// </summary>
        public static bool Matches(this UserSecurityPolicy first, UserSecurityPolicy second)
        {
            return first.Name.Equals(second.Name, StringComparison.OrdinalIgnoreCase) &&
                (
                    (string.IsNullOrEmpty(first.Value) && string.IsNullOrEmpty(second.Value)) ||
                    (first.Value.Equals(second.Value, StringComparison.OrdinalIgnoreCase))
                );
        }

        /// <summary>
        /// Check whether a user has the security policies required for a policy group.
        /// </summary>
        /// <returns>True if enrolled (has all policies), false otherwise.</returns>
        public static bool IsEnrolled(this User user, UserSecurityPolicyGroup policyGroup)
        {
            return !user.FindPolicies(policyGroup).Any(p => p == null);
        }

        /// <summary>
        /// Ensure user is enrolled in the security policy group.
        /// </summary>
        /// <param name="user">User to enroll.</param>
        /// <param name="policyGroup">User security policy group to enroll in.</param>
        public static void AddPolicies(this User user, UserSecurityPolicyGroup policyGroup)
        {
            // Add policies, if not already enrolled in all group policies.
            if (!user.IsEnrolled(policyGroup))
            {
                foreach (var policy in policyGroup.Policies)
                {
                    user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, policy.Value));
                }
                policyGroup.OnEnroll?.Invoke(user);
            }
        }

        /// <summary>
        /// Ensure user is unenrolled from the security policy group.
        /// </summary>
        /// <param name="user">User to unenroll.</param>
        /// <param name="policyGroup">User security policy group to unenroll from.</param>
        public static IEnumerable<UserSecurityPolicy> RemovePolicies(this User user, UserSecurityPolicyGroup policyGroup)
        {
            // Remove policies, only if enrolled in all group policies.
            var matches = user.FindPolicies(policyGroup);
            if (!matches.Any(p => p == null))
            {
                foreach (var policy in matches)
                {
                    user.SecurityPolicies.Remove(policy);
                    yield return policy;
                }
            }
        }

        /// <summary>
        /// Find user security policies which are part of a security policy group.
        /// </summary>
        private static IEnumerable<UserSecurityPolicy> FindPolicies(this User user, UserSecurityPolicyGroup policyGroup)
        {
            return policyGroup.Policies.Select(gp => user.SecurityPolicies.FirstOrDefault(up => up.Matches(gp)));
        }
    }
}