// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Security
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
        void OnSubscribe(User user);

        /// <summary>
        /// Callback for user unsubscription.
        /// </summary>
        void OnUnsubscribe(User user);
    }
}