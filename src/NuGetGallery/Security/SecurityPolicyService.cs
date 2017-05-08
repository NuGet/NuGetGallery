// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Service that looks up and evaluates security policies for attributed controller actions.
    /// </summary>
    public class SecurityPolicyService : ISecurityPolicyService
    {
        private static Lazy<IEnumerable<UserSecurityPolicyHandler>> _userHandlers =
            new Lazy<IEnumerable<UserSecurityPolicyHandler>>(CreateUserHandlers);

        private static Lazy<IEnumerable<IUserSecurityPolicySubscription>> _userSubscriptions =
            new Lazy<IEnumerable<IUserSecurityPolicySubscription>>(CreateUserSubscriptions);
        
        protected IEntitiesContext EntitiesContext { get; set; }

        protected SecurityPolicyService()
        {
        }

        public SecurityPolicyService(IEntitiesContext entitiesContext)
        {
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        /// <summary>
        /// Available user security policy handlers.
        /// </summary>
        protected virtual IEnumerable<UserSecurityPolicyHandler> UserHandlers
        {
            get
            {
                return _userHandlers.Value;
            }
        }

        /// <summary>
        /// Available user security policy subscriptions.
        /// </summary>
        public virtual IEnumerable<IUserSecurityPolicySubscription> UserSubscriptions
        {
            get
            {
                return _userSubscriptions.Value;
            }
        }

        /// <summary>
        /// Look up and evaluation of security policies for the specified action.
        /// </summary>
        public SecurityPolicyResult Evaluate(SecurityPolicyAction action, HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var user = httpContext.GetCurrentUser();
            foreach (var handler in UserHandlers.Where(h => h.Action == action))
            {
                var foundPolicies = user.SecurityPolicies.Where(p => p.Name.Equals(handler.Name, StringComparison.OrdinalIgnoreCase));
                if (foundPolicies.Any())
                {
                    var result = handler.Evaluate(new UserSecurityPolicyContext(httpContext, foundPolicies));
                    if (!result.Success)
                    {
                        return result;
                    }
                }
            }
            return SecurityPolicyResult.SuccessResult;
        }

        /// <summary>
        /// Check if a user is subscribed to one or more security policies.
        /// </summary>
        public bool IsSubscribed(User user, IUserSecurityPolicySubscription subscription)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (subscription == null)
            {
                throw new ArgumentNullException(nameof(subscription));
            }

            var subscribed = FindPolicies(user, subscription);
            var required = subscription.Policies;

            return required.All(rp => subscribed.Any(sp => sp.Equals(rp)));
        }

        /// <summary>
        /// Subscribe a user to one or more security policies.
        /// </summary>
        public Task SubscribeAsync(User user, IUserSecurityPolicySubscription subscription)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (subscription == null)
            {
                throw new ArgumentNullException(nameof(subscription));
            }

            if (!IsSubscribed(user, subscription))
            {
                foreach (var policy in subscription.Policies)
                {
                    user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
                }

                subscription.OnSubscribe(user);

                return EntitiesContext.SaveChangesAsync();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Unsubscribe a user from one or more security policies.
        /// </summary>
        public Task UnsubscribeAsync(User user, IUserSecurityPolicySubscription subscription)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (subscription == null)
            {
                throw new ArgumentNullException(nameof(subscription));
            }

            var matches = FindPolicies(user, subscription).ToList();
            if (matches.Any())
            {
                foreach (var policy in matches)
                {
                    user.SecurityPolicies.Remove(policy);
                    EntitiesContext.UserSecurityPolicies.Remove(policy);
                }

                subscription.OnUnsubscribe(user);

                return EntitiesContext.SaveChangesAsync();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Find user security policies which belong to a subscription.
        /// </summary>
        private static IEnumerable<UserSecurityPolicy> FindPolicies(User user, IUserSecurityPolicySubscription subscription)
        {
            return user.SecurityPolicies.Where(s => s.Subscription.Equals(subscription.SubscriptionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Registration of available user security policy handlers.
        /// </summary>
        private static IEnumerable<UserSecurityPolicyHandler> CreateUserHandlers()
        {
            yield return new RequireMinClientVersionForPushPolicy();
            yield return new RequirePackageVerifyScopePolicy();
        }

        /// <summary>
        /// Registration of available user security policy subscriptions.
        /// </summary>
        private static IEnumerable<IUserSecurityPolicySubscription> CreateUserSubscriptions()
        {
            yield return new SecurePushSubscription();
        }
    }
}