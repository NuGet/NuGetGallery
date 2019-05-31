// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery.Security
{
    public class DefaultSubscription : IUserSecurityPolicySubscription
    {
        private Lazy<List<UserSecurityPolicy>> _policies = new Lazy<List<UserSecurityPolicy>>(InitializePoliciesList, isThreadSafe: true);

        internal const string MinProtocolVersion = "4.1.0";
        internal const string Name = "Default";

        /// <summary>
        /// Subscription name.
        /// </summary>
        public string SubscriptionName => Name;

        /// <summary>
        /// Required policies for this subscription.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies => _policies.Value;

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            throw new NotSupportedException();
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            throw new NotSupportedException();
        }

        private static List<UserSecurityPolicy> InitializePoliciesList()
        {
            return new List<UserSecurityPolicy>()
            {
                new UserSecurityPolicy(RequirePackageVerifyScopePolicy.PolicyName, Name),
                RequireMinProtocolVersionForPushPolicy.CreatePolicy(Name, new NuGetVersion(MinProtocolVersion))
            };
        }
    }
}