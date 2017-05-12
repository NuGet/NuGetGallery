// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Auditing;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policies for the secure push subscription.
    /// </summary>
    public class SecurePushSubscription : IUserSecurityPolicySubscription
    {
        public const string Name = "SecurePush";
        private const string MinClientVersion = "4.1.0";
        private const int PushKeysExpirationInDays = 30;

        private IAuditingService _auditing;
        private IDiagnosticsSource _diagnostics;

        /// <summary>
        /// Subscription name.
        /// </summary>
        public string SubscriptionName
        {
            get
            {
                return Name;
            }
        }

        /// <summary>
        /// Required policies for this subscription.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies
        {
            get
            {
                yield return new UserSecurityPolicy(RequirePackageVerifyScopePolicy.PolicyName, SubscriptionName);
                yield return RequireMinClientVersionForPushPolicy.CreatePolicy(SubscriptionName, new NuGetVersion(MinClientVersion));
            }
        }

        public SecurePushSubscription(IAuditingService auditing, IDiagnosticsService diagnostics)
        {
            _auditing = auditing ?? throw new ArgumentNullException(nameof(auditing));
            _diagnostics = diagnostics?.SafeGetSource(nameof(SecurePushSubscription)) ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        /// <summary>
        /// On subscribe, set API keys with push capability to expire in 30 days.
        /// </summary>
        /// <param name="context"></param>
        public async Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            var pushKeys = context.User.Credentials.Where(c =>
                CredentialTypes.IsApiKey(c.Type) &&
                (
                    c.Scopes.Count == 0 ||
                    c.Scopes.Any(s =>
                        s.AllowedAction.Equals(NuGetScopes.PackagePush, StringComparison.OrdinalIgnoreCase) ||
                        s.AllowedAction.Equals(NuGetScopes.PackagePushVersion, StringComparison.OrdinalIgnoreCase)
                        ))
                );

            var expires = DateTime.UtcNow.AddDays(PushKeysExpirationInDays);
            foreach (var key in pushKeys)
            {
                if (!key.Expires.HasValue || key.Expires > expires)
                {
                    await _auditing.SaveAuditRecordAsync(
                        new UserAuditRecord(context.User, AuditedUserAction.ExpireCredential, key));

                    key.Expires = expires;
                }
            }
            
            _diagnostics.Information($"Expiring {pushKeys.Count()} keys with push capability for user '{context.User.Username}'.");
        }
        
        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }
    }
}