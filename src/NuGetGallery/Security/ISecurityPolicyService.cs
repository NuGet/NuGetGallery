// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Service for managing security policies.
    /// </summary>
    public interface ISecurityPolicyService
    {
        /// <summary>
        /// Available user security policy subscriptions.
        /// </summary>
        IEnumerable<IUserSecurityPolicySubscription> UserSubscriptions { get; }

        /// <summary>
        /// Check if a user is subscribed to one or more security policies.
        /// </summary>
        bool IsSubscribed(User user, string subscriptionName);

        /// <summary>
        /// Check if a user is subscribed to one or more security policies.
        /// </summary>
        bool IsSubscribed(User user, IUserSecurityPolicySubscription subscription);

        /// <summary>
        /// Subscribe a user to one or more security policies.
        /// </summary>
        Task<bool> SubscribeAsync(User user, string subscriptionName);

        /// <summary>
        /// Subscribe a user to one or more security policies.
        /// </summary>
        Task<bool> SubscribeAsync(User user, IUserSecurityPolicySubscription subscription);

        /// <summary>
        /// Unsubscribe a user from one or more security policies.
        /// </summary>
        Task UnsubscribeAsync(User user, string subscriptionName);

        /// <summary>
        /// Unsubscribe a user from one or more security policies.
        /// </summary>
        Task UnsubscribeAsync(User user, IUserSecurityPolicySubscription subscription);

        /// <summary>
        /// Evaluate any security policies that may apply to the current context.
        /// </summary>
        /// <param name="action">Security policy action.</param>
        /// <param name="httpContext">Http context.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="SecurityPolicyResult" />
        /// instance.</returns>
        Task<SecurityPolicyResult> EvaluateAsync(SecurityPolicyAction action, HttpContextBase httpContext);
    }
}