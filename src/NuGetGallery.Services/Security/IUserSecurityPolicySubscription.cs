// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.Security
{
    /// <summary>
    /// One or more security policies which a user can subscribe to.
    /// </summary>
    public interface IUserSecurityPolicySubscription
    {
        /// <summary>
        /// Subscription name.
        /// </summary>
        string SubscriptionName { get; }

        /// <summary>
        /// Required policies.
        /// </summary>
        IEnumerable<UserSecurityPolicy> Policies { get; }

        /// <summary>
        /// Callback for user subscription.
        /// </summary>
        Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context);

        /// <summary>
        /// Callback for user unsubscription.
        /// </summary>
        Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context);
    }
}