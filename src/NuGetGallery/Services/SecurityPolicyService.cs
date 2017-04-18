// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Auditing;
using NuGetGallery.Auditing.AuditedEntities;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    /// <summary>
    /// Service to manage subscription and enforcement of security policies.
    /// </summary>
    public class SecurityPolicyService : ISecurityPolicyService
    {
        internal IAuditingService Auditing { get; set; }

        private IEntitiesContext Entities { get; set; }

        private IDiagnosticsSource Trace { get; set; }

        public SecurityPolicyService(IEntitiesContext entities, IAuditingService auditing, IDiagnosticsService diagnostics)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }
            if (auditing == null)
            {
                throw new ArgumentNullException(nameof(auditing));
            }
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }
            
            Entities = entities;
            Auditing = auditing;

            Trace = diagnostics.SafeGetSource("SecurityPolicyService");
        }

        /// <summary>
        /// Subscribes a user to a security policy.
        /// </summary>
        /// <param name="user">User to subscribe.</param>
        /// <param name="policy">Policy instance.</param>
        /// <returns>Awaitable task.</returns>
        public Task AddSecurityPolicyAsync(User user, SecurityPolicy policy)
        {
            Trace.Verbose($"Adding Security policy '{policy.GetType().Name}' for user '{user.Username}'. ");

            var packageVerificationKeysPolicy = policy as PackageVerificationKeysPolicy;
            if (packageVerificationKeysPolicy != null)
            {
                return AddSecurityPolicyAsync(user, packageVerificationKeysPolicy);
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Subscribes a user to the PackageVerificationKeys security policy.
        /// </summary>
        /// <param name="user">User to subscribe.</param>
        /// <param name="securityPolicy">Policy instance.</param>
        /// <returns>Awaitable task.</returns>
        private async Task AddSecurityPolicyAsync(User user, PackageVerificationKeysPolicy policy)
        {
            user.SecurityPolicies.Add(policy);
            
            await Auditing.SaveAuditRecordAsync(
                new PackageVerificationKeysPolicyAuditRecord(
                    UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_AddPolicy,
                    user.Username,
                    policy));
            
            // On subscribing, this policy requires expiration of API keys with push capabilities.
            var expiredCount = 0;
            foreach (var credential in user.Credentials)
            {
                if (credential.IsApiKeyWithPushCapability())
                {
                    await Auditing.SaveAuditRecordAsync(
                        new UserAuditRecord(user, AuditedUserAction.ExpireCredential, credential));

                    // Provide 1-week window before expiration to allow setup of new keys.
                    credential.Expires = DateTime.UtcNow.AddDays(7);
                    expiredCount++;
                }
            }

            Trace.Verbose($"Expiring '{expiredCount}' API keys with push scopes in 1 week for policy '{nameof(PackageVerificationKeysPolicy)}'.");

            await Entities.SaveChangesAsync();
        }

        public async Task<SecurityPolicyResult> CanCreatePackageAsync(User user, HttpContextBase context, string id, string version)
        {
            string errorMessage = null;
            var policy = GetPackageVerificationKeysPolicy(user);
            
            if (policy != null)
            {
                // Evaluate policy requirements.
                if (!context.Request.IsNuGetClientVersionOrHigher(policy.MinClientVersion))
                {
                    errorMessage = String.Format(Strings.PackageVerificationKeysPolicyError, policy.MinClientVersion);
                }

                // Audit for users subscribed to the policy.
                await Auditing.SaveAuditRecordAsync(
                    new PackageVerificationKeysPolicyAuditRecord(
                        UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_CreatePackage,
                        user.Username,
                        policy,
                        errorMessage,
                        pushedPackage: new AuditedPackageIdentifier(id, version)));
            }

            return new SecurityPolicyResult(policy, errorMessage);
        }

        public async Task<SecurityPolicyResult> CanVerifyPackageKeyAsync(User user, HttpContextBase context, string id, string version)
        {
            string errorMessage = null;
            var policy = GetPackageVerificationKeysPolicy(user);
            
            if (policy != null)
            {
                // Evaluate policy requirements.
                var credential = user.GetCurrentCredential(context.User.Identity);
                if (!CredentialTypes.IsPackageVerificationApiKey(credential.Type))
                {
                    errorMessage = String.Format(Strings.PackageVerificationKeysPolicyError, policy.MinClientVersion);
                }

                // Audit for users subscribed to the policy.
                await Auditing.SaveAuditRecordAsync(
                    new PackageVerificationKeysPolicyAuditRecord(
                        UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_VerifyPackageKey,
                        user.Username,
                        policy,
                        errorMessage,
                        pushedPackage: new AuditedPackageIdentifier(id, version)));
            }
            
            return new SecurityPolicyResult(policy, errorMessage);
        }

        private PackageVerificationKeysPolicy GetPackageVerificationKeysPolicy(User user)
        {
            // In case of multiple, use the max of the min client versions required.
            return user.SecurityPolicies.OfType<PackageVerificationKeysPolicy>()
                .OrderByDescending(p => p.MinClientVersion)
                .FirstOrDefault();
        }
    }
}