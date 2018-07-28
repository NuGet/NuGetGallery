// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy that requires packages pushed by this user to be compliant with certain package metadata policies.
    /// </summary>
    public class RequirePackageMetadataCompliancePolicy : PackageSecurityPolicyHandler
    {
        public const string PolicyName = nameof(RequirePackageMetadataCompliancePolicy);

        public string SubscriptionName => Name;

        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public RequirePackageMetadataCompliancePolicy()
                : base(PolicyName, SecurityPolicyAction.PackagePush)
        {
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

            // This particular package policy assumes the existence of a particular user.
            // Succeed silently (effectively ignoring this policy when enabled) when that user does not exist.
            var state = GetState(context);
            var requiredCoOwner = context.UserService.FindByUsername(state.RequiredCoOwnerUsername);
            if (requiredCoOwner == null)
            {
                // This may happen on gallery deployments that don't have this particular user.
                return SecurityPolicyResult.SuccessResult;
            }

            // Evaluate package metadata validations
            if (!IsPackageMetadataCompliant(context.Package, state, out var complianceFailures))
            {
                // Package policy not met.
                return SecurityPolicyResult.CreateErrorResult(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        state.ErrorMessageFormat,
                        Environment.NewLine + string.Join(Environment.NewLine, complianceFailures)));
            }

            // Automatically add the required co-owner when metadata is compliant.
            if (!context.Package.PackageRegistration.Owners.Select(o => o.Username).Contains(state.RequiredCoOwnerUsername, StringComparer.OrdinalIgnoreCase))
            {
                // This will also mark the package as verified if the prefix has been reserved by the co-owner.
                // The entities context is committed later as a single atomic transaction (see PackageUploadService).
                await context.PackageOwnershipManagementService.AddPackageOwnerAsync(context.Package.PackageRegistration, requiredCoOwner, commitChanges: false);
            }

            // If the PackageRegistration is not marked as verified,
            // the account pushing the package has not registered the prefix yet.
            if (!context.Package.PackageRegistration.IsVerified)
            {
                return SecurityPolicyResult.CreateWarningResult(Strings.SecurityPolicy_RequirePackagePrefixReserved);
            }

            // All good!
            return SecurityPolicyResult.SuccessResult;
        }

        public static UserSecurityPolicy CreatePolicy(
            string subscription, 
            string requiredCoOwnerUsername, 
            string[] allowedCopyrightNotices,
            bool isLicenseUrlRequired,
            bool isProjectUrlRequired,
            string errorMessageFormat)
        {
            var value = JsonConvert.SerializeObject(new State()
            {
                RequiredCoOwnerUsername = requiredCoOwnerUsername,
                AllowedCopyrightNotices = allowedCopyrightNotices,
                IsLicenseUrlRequired = isLicenseUrlRequired,
                IsProjectUrlRequired = isProjectUrlRequired,
                ErrorMessageFormat = errorMessageFormat
            });

            return new UserSecurityPolicy(PolicyName, subscription, value);
        }

        private bool IsPackageMetadataCompliant(Package package, State state, out IList<string> complianceFailures)
        {
            complianceFailures = new List<string>();

            // Author validation
            if (!package.FlattenedAuthors
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Contains(state.RequiredCoOwnerUsername, StringComparer.InvariantCultureIgnoreCase))
            {
                complianceFailures.Add(string.Format(CultureInfo.CurrentCulture, Strings.SecurityPolicy_RequiredAuthorMissing, state.RequiredCoOwnerUsername));
            }

            // Copyright validation
            if (!state.AllowedCopyrightNotices.Contains(package.Copyright))
            {
                complianceFailures.Add(Strings.SecurityPolicy_CopyrightNotCompliant);
            }

            // LicenseUrl validation
            if (state.IsLicenseUrlRequired && string.IsNullOrWhiteSpace(package.LicenseUrl))
            {
                complianceFailures.Add(Strings.SecurityPolicy_RequiredLicenseUrlMissing);
            }

            // ProjectUrl validation
            if (state.IsProjectUrlRequired && string.IsNullOrWhiteSpace(package.ProjectUrl))
            {
                complianceFailures.Add(Strings.SecurityPolicy_RequiredProjectUrlMissing);
            }

            return !complianceFailures.Any();
        }


        /// <summary>
        /// Retrieve the policy state.
        /// </summary>
        private State GetState(UserSecurityPolicyEvaluationContext context)
        {
            var policyStates = context.Policies
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => JsonConvert.DeserializeObject<State>(p.Value));

            // TODO: what if there are multiple?
            return policyStates.First();
        }

        public class State
        {
            [JsonProperty("u")]
            public string RequiredCoOwnerUsername { get; set; }

            [JsonProperty("copy")]
            public string[] AllowedCopyrightNotices { get; set; }

            [JsonProperty("licUrlReq")]
            public bool IsLicenseUrlRequired { get; set; }
            
            [JsonProperty("projUrlReq")]
            public bool IsProjectUrlRequired { get; set; }

            [JsonProperty("error")]
            public string ErrorMessageFormat { get; set; }
        }
    }
}