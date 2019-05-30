// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.Security
{
    public abstract class RequiredSignerPolicy : UserSecurityPolicyHandler, IUserSecurityPolicySubscription
    {
        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public string SubscriptionName => Name;

        public RequiredSignerPolicy(string policyName, SecurityPolicyAction action)
            : base(policyName, action)
        {
            Policies = new[]
            {
                new UserSecurityPolicy(policyName, policyName)
            };
        }

        public override Task<SecurityPolicyResult> EvaluateAsync(UserSecurityPolicyEvaluationContext context)
        {
            throw new NotImplementedException();  // Not used.
        }

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }
    }
}