﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy that requires packages pushed by this user to be compliant with Microsoft package policy.
    /// </summary>
    public class RequireMicrosoftPackageCompliancePolicy : PackageSecurityPolicyHandler, IUserSecurityPolicySubscription
    {
        public const string PolicyName = nameof(RequireMicrosoftPackageCompliancePolicy);

        internal const string MicrosoftUsername = "Microsoft";

        public string SubscriptionName => Name;

        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public RequireMicrosoftPackageCompliancePolicy()
                : base(PolicyName, SecurityPolicyAction.PackagePush)
        {
            Policies = new[]
            {
                new UserSecurityPolicy(PolicyName, PolicyName)
            };
        }

        /// <summary>
        /// Evaluate if this package compliance policy is met.
        /// </summary>
        public override async Task<SecurityPolicyResult> EvaluateAsync(PackageSecurityPolicyEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // This particular package policy assumes the existence of a 'Microsoft' user.
            // Succeed silently (effectively ignoring this policy when enabled) when that user does not exist.
            var microsoftUser = context.EntitiesContext.Users.SingleOrDefault(u => u.Username == MicrosoftUsername);
            if (microsoftUser == null)
            {
                // This may happen on gallery deployments that don't have a 'Microsoft' user.
                return SecurityPolicyResult.SuccessResult;
            }

            // If the package being evaluated has not been registered yet (new package ID),
            // this policy requires the prefix to be reserved.
            var isWarning = false;
            if (context.ExistingPackageRegistration == null)
            {
                // We are evaluating a newly pushed package with a new ID.
                var packageRegistrationId = context.Package.PackageRegistration.Id;

                // If the generated PackageRegistration is not marked as verified (by `PackageUploadService.GeneratePackageAsync`),
                // the account pushing the package has not registered the prefix yet.
                if (!context.Package.PackageRegistration.IsVerified)
                {
                    // The owner has not reserved the prefix. Check whether the Microsoft user has.
                    var prefixIsReservedByMicrosoft = context.ReservedNamespaceService.ShouldMarkNewPackageIdVerified(
                        microsoftUser, 
                        packageRegistrationId, 
                        out var ownedMatchingReservedNamespaces);

                    // If the prefix has not been reserved by the 'Microsoft' user either,
                    // then generate a warning which will result in an alternate email being sent to the package owners when validation of the metadata succeeds, and the package is pushed.
                    isWarning = !prefixIsReservedByMicrosoft;
                }
            }

            // Evaluate Microsoft metadata validations
            var packageIsCompliant = IsPackageMetadataCompliant(context.Package);
            if (!packageIsCompliant)
            {
                // Microsoft package policy not met.
                return SecurityPolicyResult.CreateErrorResult(Strings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush);
            }

            // Automatically add 'Microsoft' as co-owner when metadata is compliant.
            if (!context.Package.PackageRegistration.Owners.Select(o => o.Username).Contains(MicrosoftUsername, StringComparer.OrdinalIgnoreCase))
            {
                // This will also mark the package as verified if the prefix has been reserved by Microsoft.
                // The entities context is committed later as a single atomic transaction (see PackageUploadService).
                await context.PackageOwnershipManagementService.AddPackageOwnerAsync(context.Package.PackageRegistration, microsoftUser, commitChanges: false);
            }

            if (isWarning)
            {
                return SecurityPolicyResult.CreateWarningResult(Strings.SecurityPolicy_RequirePackagePrefixReserved);
            }

            // All good!
            return SecurityPolicyResult.SuccessResult;
        }

        private bool IsPackageMetadataCompliant(Package package)
        {
            // Author validation
            if (!package.FlattenedAuthors
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Contains(MicrosoftUsername, StringComparer.InvariantCultureIgnoreCase))
            {
                return false;
            }

            // Copyright validation
            if (!string.Equals(package.Copyright, "(c) Microsoft Corporation. All rights reserved.")
                && !string.Equals(package.Copyright, "© Microsoft Corporation. All rights reserved."))
            {
                return false;
            }

            // LicenseUrl validation
            if (string.IsNullOrWhiteSpace(package.LicenseUrl))
            {
                return false;
            }

            // ProjectUrl validation
            if (string.IsNullOrWhiteSpace(package.ProjectUrl))
            {
                return false;
            }

            // If we made it this far, the package metadata is compliant.
            return true;
        }

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }
    }
}