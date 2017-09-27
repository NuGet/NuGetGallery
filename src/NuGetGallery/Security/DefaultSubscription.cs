// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGetGallery.Security
{
    public class DefaultSubscription : IUserSecurityPolicySubscription
    {
        internal const string MinProtocolVersion = "4.1.0";

        /// <summary>
        /// Subscription name.
        /// </summary>
        public string SubscriptionName => "Default";

        /// <summary>
        /// Required policies for this subscription.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies
        {
            get
            {
                yield return new UserSecurityPolicy(RequirePackageVerifyScopePolicy.PolicyName, SubscriptionName);
                yield return RequireMinProtocolVersionForPushPolicy.CreatePolicy(SubscriptionName, new NuGetVersion(MinProtocolVersion));
            }
        }

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            throw new NotSupportedException();
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            throw new NotSupportedException();
        }
    }
}