// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery.Security
{
    public class MicrosoftTeamSubscription : IUserSecurityPolicySubscription
    {
        private Lazy<List<UserSecurityPolicy>> _policies = new Lazy<List<UserSecurityPolicy>>(InitializePoliciesList, isThreadSafe: true);

        internal const string MicrosoftUsername = "Microsoft";
        internal const string Name = nameof(MicrosoftTeamSubscription);

        public string SubscriptionName => Name;

        public MicrosoftTeamSubscription()
        {
        }

        public IEnumerable<UserSecurityPolicy> Policies => _policies.Value;

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            // Todo:
            // Maybe we should enumerate through the user's packages and add Microsoft as a package owner if the package passes the metadata requirements when a user is onboarded to this policy.
            // We should also unlock the package if it is locked as part of adding Microsoft as co-owner.
            return Task.CompletedTask;
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }

        private static List<UserSecurityPolicy> InitializePoliciesList()
        {
            return new List<UserSecurityPolicy>()
            {
                RequirePackageMetadataCompliancePolicy.CreatePolicy(
                    Name, 
                    MicrosoftUsername,
                    allowedCopyrightNotices: new string[] 
                    {
                        "(c) Microsoft Corporation. All rights reserved.",
                        "© Microsoft Corporation. All rights reserved."
                    },
                    isLicenseUrlRequired: true,
                    isProjectUrlRequired: true,
                    errorMessageFormat: Strings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush)
            };
        }
    }
}