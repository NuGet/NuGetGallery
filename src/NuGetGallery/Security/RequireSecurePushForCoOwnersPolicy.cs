// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Subscribable policy that propagates secure push policies to new package co-owners.
    /// </summary>
    public class RequireSecurePushForCoOwnersPolicy : UserSecurityPolicyHandler, IUserSecurityPolicySubscription
    {
        public const string _SubscriptionName = "SecurePushForCoOwners";
        public const string PolicyName = nameof(RequireSecurePushForCoOwnersPolicy);

        public string SubscriptionName => _SubscriptionName;

        public IEnumerable<UserSecurityPolicy> Policies
        {
            get
            {
                yield return new UserSecurityPolicy(PolicyName, SubscriptionName);
            }
        }

        public RequireSecurePushForCoOwnersPolicy()
            : base(PolicyName, SecurityPolicyAction.ManagePackageOwners)
        {
        }

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }

        public override SecurityPolicyResult Evaluate(UserSecurityPolicyEvaluationContext context)
        {
            return SecurityPolicyResult.SuccessResult;
        }

        public static bool IsSubscribed(User user)
        {
            return user.SecurityPolicies.Any(p => p.Name.Equals(RequireSecurePushForCoOwnersPolicy.PolicyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}