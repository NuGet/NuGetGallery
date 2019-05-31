// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Context providing user security policy subscriptions with resources necessary for subscription.
    /// </summary>
    public class UserSecurityPolicySubscriptionContext
    {
        public ISecurityPolicyService PolicyService { get; }

        public User User { get; }

        public UserSecurityPolicySubscriptionContext(ISecurityPolicyService policyService, User user)
        {
            PolicyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
            User = user ?? throw new ArgumentNullException(nameof(user));
        }
    }
}