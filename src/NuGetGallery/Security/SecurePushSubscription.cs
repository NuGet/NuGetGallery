// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;
using NuGetGallery.Authentication;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policies for the secure push subscription.
    /// </summary>
    public class SecurePushSubscription : IUserSecurityPolicySubscription
    {
        public const string SubscriptionName = "SecurePush";
        public const string MinClientVersion = "4.1.0";

        /// <summary>
        /// Subscription name.
        /// </summary>
        public string Name
        {
            get
            {
                return SubscriptionName;
            }
        }

        /// <summary>
        /// Required policies for this subscription.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies
        {
            get
            {
                yield return new UserSecurityPolicy(RequirePackageVerifyScopePolicy.PolicyName, Name);
                yield return RequireMinClientVersionForPushPolicy.CreatePolicy(Name, new NuGetVersion(MinClientVersion));
            }
        }

        public void OnSubscribe(User user)
        {
            SetPushApiKeysToExpire(user);
        }
        
        public void OnUnsubscribe(User user)
        {
        }

        /// <summary>
        /// Expire API keys with push capability on secure push enrollment.
        /// </summary>
        private static void SetPushApiKeysToExpire(User user, int expirationInDays = 7)
        {
            var pushKeys = user.Credentials.Where(c =>
                CredentialTypes.IsApiKey(c.Type) &&
                (
                    c.Scopes.Count == 0 ||
                    c.Scopes.Any(s =>
                        s.AllowedAction.Equals(NuGetScopes.PackagePush, StringComparison.OrdinalIgnoreCase) ||
                        s.AllowedAction.Equals(NuGetScopes.PackagePushVersion, StringComparison.OrdinalIgnoreCase)
                        ))
                );

            foreach (var key in pushKeys)
            {
                var expires = DateTime.UtcNow.AddDays(expirationInDays);
                if (!key.Expires.HasValue || key.Expires > expires)
                {
                    key.Expires = expires;
                }
            }
        }
    }
}