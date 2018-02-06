// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Service that looks up and evaluates security policies for attributed controller actions.
    /// </summary>
    public class SecurityPolicyService : ISecurityPolicyService
    {
        private static Lazy<IEnumerable<UserSecurityPolicyHandler>> _userHandlers =
            new Lazy<IEnumerable<UserSecurityPolicyHandler>>(CreateUserHandlers);

        protected IEntitiesContext EntitiesContext { get; set; }

        protected IAuditingService Auditing { get; set; }

        protected IDiagnosticsSource Diagnostics { get; set; }

        protected IAppConfiguration Configuration { get; set; }

        protected SecurePushSubscription SecurePush { get; set; }

        protected IUserSecurityPolicySubscription DefaultSubscription { get; set; }

        protected RequireSecurePushForCoOwnersPolicy SecurePushForCoOwners { get; set; }

        protected SecurityPolicyService()
        {
        }

        public SecurityPolicyService(IEntitiesContext entitiesContext, IAuditingService auditing, IDiagnosticsService diagnostics, IAppConfiguration configuration,
            SecurePushSubscription securePush = null, RequireSecurePushForCoOwnersPolicy securePushForCoOwners = null)
        {
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            Auditing = auditing ?? throw new ArgumentNullException(nameof(auditing));
            SecurePush = securePush;
            SecurePushForCoOwners = securePushForCoOwners;

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Diagnostics = diagnostics.SafeGetSource(nameof(SecurityPolicyService));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            DefaultSubscription = new DefaultSubscription();
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
                yield return SecurePush;
                yield return SecurePushForCoOwners;
            }
        }

        /// <summary>
        /// Look up and evaluation of security policies for the specified action.
        /// </summary>
        public async Task<SecurityPolicyResult> EvaluateUserPoliciesAsync(
            SecurityPolicyAction action,
            HttpContextBase httpContext = null)
        {
            // Evaluate default policies
            if (Configuration.EnforceDefaultSecurityPolicies)
            {
                var defaultPolicies = DefaultSubscription.Policies;

                var result = await EvaluateUserPoliciesInternalAsync(action, httpContext, defaultPolicies, auditSuccess: false);
                if (!result.Success)
                {
                    return result;
                }
            }

            // Evaluate user specific policies
            return await EvaluateUserPoliciesInternalAsync(action, httpContext, auditSuccess: true);
        }

        /// <summary>
        ///  Look up and evaluation of organization security policies for the specified action.
        /// </summary>
        public Task<SecurityPolicyResult> EvaluateOrganizationPoliciesAsync(
            SecurityPolicyAction action,
            Organization organization,
            User account)
        {
            return EvaluateInternalAsync(action, organization.SecurityPolicies, organization, account, auditSuccess: true);
        }

        private Task<SecurityPolicyResult> EvaluateUserPoliciesInternalAsync(
            SecurityPolicyAction action,
            HttpContextBase httpContext,
            IEnumerable<UserSecurityPolicy> policies = null,
            bool auditSuccess = true)
        {
            httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));

            var account = httpContext.GetCurrentUser();
            policies = policies ?? account.SecurityPolicies;
            return EvaluateInternalAsync(action, policies, account, account, httpContext, auditSuccess);
        }

        private async Task<SecurityPolicyResult> EvaluateInternalAsync(
            SecurityPolicyAction action,
            IEnumerable<UserSecurityPolicy> policies,
            User sourceAccount,
            User targetAccount,
            HttpContextBase httpContext = null,
            bool auditSuccess = true)
        {
            var relevantHandlers = UserHandlers.Where(h => h.Action == action).ToList();

            foreach (var handler in relevantHandlers)
            {
                var foundPolicies = policies.Where(p => p.Name.Equals(handler.Name, StringComparison.OrdinalIgnoreCase)).ToList();

                if (foundPolicies.Any())
                {
                    var context = new UserSecurityPolicyEvaluationContext(foundPolicies, sourceAccount, targetAccount, httpContext);
                    var result = handler.Evaluate(context);

                    if (auditSuccess || !result.Success)
                    {
                        await Auditing.SaveAuditRecordAsync(new UserSecurityPolicyAuditRecord(
                        context.TargetAccount.Username, GetAuditAction(action), foundPolicies, result.Success, result.ErrorMessage));
                    }

                    if (!result.Success)
                    {
                        Diagnostics.Information(
                        $"Security policy from subscription '{foundPolicies.First().Subscription}' - '{handler.Name}' failed with error '{result.ErrorMessage}'.");

                        return result;
                    }
                }
            }

            return SecurityPolicyResult.SuccessResult;
        }

        private AuditedSecurityPolicyAction GetAuditAction(SecurityPolicyAction policyAction)
        {
            switch (policyAction)
            {
                case SecurityPolicyAction.PackagePush:
                    return AuditedSecurityPolicyAction.Create;
                case SecurityPolicyAction.PackageVerify:
                    return AuditedSecurityPolicyAction.Verify;
                case SecurityPolicyAction.JoinOrganization:
                    return AuditedSecurityPolicyAction.JoinOrganization;
                default:
                    throw new NotSupportedException($"Policy action '{nameof(policyAction)}' is not supported");
            }
        }

        /// <summary>
        /// Check if a user is subscribed to one or more security policies.
        /// </summary>
        public bool IsSubscribed(User user, string subscriptionName)
        {
            if (string.IsNullOrEmpty(subscriptionName))
            {
                throw new ArgumentException(nameof(subscriptionName));
            }

            var subscription = UserSubscriptions.FirstOrDefault(s => s.SubscriptionName.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
            if (subscription == null)
            {
                throw new NotSupportedException($"Subscription '{subscriptionName}' not found.");
            }

            return IsSubscribed(user, subscription);
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
        public Task<bool> SubscribeAsync(User user, string subscriptionName)
        {
            if (string.IsNullOrEmpty(subscriptionName))
            {
                throw new ArgumentException(nameof(subscriptionName));
            }

            var subscription = UserSubscriptions.FirstOrDefault(s => s.SubscriptionName.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
            if (subscription == null)
            {
                throw new NotSupportedException($"Subscription '{subscriptionName}' not found.");
            }

            return SubscribeAsync(user, subscription);
        }

        /// <summary>
        /// Subscribe a user to one or more security policies.
        /// </summary>
        /// <returns>True if user was subscribed, false if not (i.e., was already subscribed).</returns>
        public async Task<bool> SubscribeAsync(User user, IUserSecurityPolicySubscription subscription)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (subscription == null)
            {
                throw new ArgumentNullException(nameof(subscription));
            }

            if (IsSubscribed(user, subscription))
            {
                Diagnostics.Information($"User is already subscribed to '{subscription.SubscriptionName}'.");

                return false;
            }
            else
            {
                foreach (var policy in subscription.Policies)
                {
                    user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
                }

                await subscription.OnSubscribeAsync(new UserSecurityPolicySubscriptionContext(this, user));

                await Auditing.SaveAuditRecordAsync(
                    new UserAuditRecord(user, AuditedUserAction.SubscribeToPolicies, subscription.Policies));

                await EntitiesContext.SaveChangesAsync();

                Diagnostics.Information($"User is now subscribed to '{subscription.SubscriptionName}'.");

                return true;
            }
        }

        /// <summary>
        /// Unsubscribe a user from one or more security policies.
        /// </summary>
        public Task UnsubscribeAsync(User user, string subscriptionName)
        {
            if (string.IsNullOrEmpty(subscriptionName))
            {
                throw new ArgumentException(nameof(subscriptionName));
            }

            var subscription = UserSubscriptions.FirstOrDefault(s => s.SubscriptionName.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
            if (subscription == null)
            {
                throw new NotSupportedException($"Subscription '{subscriptionName}' not found.");
            }

            return UnsubscribeAsync(user, subscription);
        }

        /// <summary>
        /// Unsubscribe a user from one or more security policies.
        /// </summary>
        public async Task UnsubscribeAsync(User user, IUserSecurityPolicySubscription subscription)
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

                await subscription.OnUnsubscribeAsync(new UserSecurityPolicySubscriptionContext(this, user));

                await Auditing.SaveAuditRecordAsync(
                    new UserAuditRecord(user, AuditedUserAction.UnsubscribeFromPolicies, subscription.Policies));

                await EntitiesContext.SaveChangesAsync();

                Diagnostics.Information($"User is now unsubscribed from '{subscription.SubscriptionName}'.");
            }
            else
            {
                Diagnostics.Information($"User is already unsubscribed from '{subscription.SubscriptionName}'.");
            }
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
            yield return new RequireMinProtocolVersionForPushPolicy();
            yield return new RequireOrganizationTenantPolicy();
        }
   }
}