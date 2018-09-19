// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy that requires packages pushed by this user to have a prefix reservation.
    /// </summary>
    public class RequirePrefixReservationPolicy : SecurityPolicyHandler<UserSecurityPolicyEvaluationContext>
    {
        public const string PolicyName = nameof(RequirePrefixReservationPolicy);

        public string SubscriptionName => Name;

        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public RequirePrefixReservationPolicy()
                : base(PolicyName, SecurityPolicyAction.PackagePush)
        {
            Policies = new[]
            {
                new UserSecurityPolicy(PolicyName, PolicyName)
            };
        }

        /// <summary>
        /// Evaluate if this user security policy is met.
        /// </summary>
        public override Task<SecurityPolicyResult> EvaluateAsync(UserSecurityPolicyEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.Policies
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => JsonConvert.DeserializeObject<State>(p.Value))
                .Single(); // TODO: what-if/how-to handle multiple values?

            // This particular package policy assumes the existence of a particular user.
            // Succeed silently (effectively ignoring this policy when enabled) when that user does not exist.
            var coOwner = context.EntitiesContext.Users.SingleOrDefault(u => u.Username == state.CoOwnerUsername);
            if (coOwner == null)
            {
                // This may happen on gallery deployments that don't have this particular user.
                return Task.FromResult(SecurityPolicyResult.SuccessResult);
            }

            // We are evaluating a newly pushed package with a new ID.
            var packageRegistrationId = context.Package.PackageRegistration.Id;

            // Check if the account pushing the package has registered the prefix.
            var isVerifiedByUserAccount = context.ReservedNamespaceService.ShouldMarkNewPackageIdVerified(context.CurrentUser, packageRegistrationId, out var _);
            if (!isVerifiedByUserAccount)
            {
                // The owner has not reserved the prefix. Check whether the Microsoft user has.
                var prefixIsReservedByMicrosoft = context.ReservedNamespaceService.ShouldMarkNewPackageIdVerified(coOwner, packageRegistrationId, out var _);

                // If the prefix has not been reserved by the 'Microsoft' user either,
                // then generate a warning which will result in an alternate email being sent to the package owners when validation of the metadata succeeds, and the package is pushed.
                if (!prefixIsReservedByMicrosoft)
                {
                    return Task.FromResult(SecurityPolicyResult.CreateWarningResult(Strings.SecurityPolicy_RequirePackagePrefixReserved));
                }
            }

            // All good!
            return Task.FromResult(SecurityPolicyResult.SuccessResult);
        }

        /// <summary>
        /// User security policy that requires packages pushed by this user to have a prefix reservation by either the account pushing the package, or the specified <paramref name="coOwnerUsername"/>.
        /// </summary>
        public static UserSecurityPolicy CreatePolicy(string subscription, string coOwnerUsername)
        {
            var value = JsonConvert.SerializeObject(new State()
            {
                CoOwnerUsername = coOwnerUsername
            });

            return new UserSecurityPolicy(PolicyName, subscription, value);
        }

        public class State
        {
            [JsonProperty("c")]
            public string CoOwnerUsername { get; set; }
        }
    }
}