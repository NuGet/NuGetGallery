// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Security
{
    public static class SecurePushMessages
    {
        private static string PolicyDescriptions = $@"
  1. All co-owners must use client version '{SecurePushSubscription.MinClientVersion}' or higher to push all of their packages.
  2. All existing push API keys for co-owners new to this policy will expire in {SecurePushSubscription.PushKeysExpirationInDays} days.
";
        /* Policy notice in the Web UI confirmation dialog (PackagesController.ManagePackageOwners) */

        /// <summary>
        /// Scenario: Confirm addition of 'Microsoft' (propagating) as owner of current user's package.
        /// </summary>
        public static string ConfirmationOfPoliciesRequiredByNewPendingCoOwner(User newOwner)
        {
            return $@"User '{newOwner.Username}' has the following requirements that will be enforced for all co-owners once the user accepts ownership of this package:

{PolicyDescriptions}

Note this step cannot be easily undone. If you are unsure and/or need more information, please contact support@nuget.org.
";
        }

        /// <summary>
        /// Scenario: Confirm addition of user as owner of current user's package where 'Microsoft' (propagating) is a co-owner.
        /// </summary>
        public static string ConfirmationOfPoliciesRequiredByCoOwners(IEnumerable<string> usernames, User newOwner)
        {
            var prefix = GetPropagatorPrefix(usernames, "Owner");
            return $@"{prefix} the following requirements that will be enforced for user '{newOwner.Username}' once the user accepts ownership of this package:

{PolicyDescriptions}

Note this step cannot be easily undone. If you are unsure and/or need more information, please contact support@nuget.org.
";
        }

        /// <summary>
        /// Scenario: Confirm addition of user as owner of current user's package where 'Microsoft' (propagating) is a pending co-owner.
        /// </summary>
        public static string ConfirmationOfPoliciesRequiredByPendingCoOwners(IEnumerable<string> usernames, User newOwner)
        {
            var prefix = GetPropagatorPrefix(usernames, "Pending owner");
            return $@"{prefix} the following requirements that will be enforced for all co-owners, including '{newOwner.Username}', once they accept ownership of this package:

{PolicyDescriptions}

Note this step cannot be easily undone. If you are unsure and/or need more information, please contact support@nuget.org.
";
        }

        /* Policy notice in the ownership confirmation email sent to the new owner (JsonApiController.AddPackageOwner) */

        /// <summary>
        /// Scenario: Email 'Microsoft' (propagating) with link to confirm ownership of other user(s)' package.
        /// </summary>
        public static string NoticeOfPoliciesRequiredByNewPendingCoOwner(User newOwner)
        {
            return $@"Note: The following policies will be enforced on package co-owners once you accept this request. If you are unsure and/or need more information, please contact support@nuget.org.
{PolicyDescriptions}
";
        }

        /// <summary>
        /// Scenario: Email user with link to confirm ownership of package with 'Microsoft' (propagating) owner.
        /// </summary>
        public static string NoticeOfPoliciesRequiredByCoOwners(IEnumerable<string> usernames)
        {
            var prefix = GetPropagatorPrefix(usernames, "Owner");
            return $@"Note: {prefix} the following policies that will be enforced on your account once you accept this request. If you are unsure and/or need more information, please contact support@nuget.org.
{PolicyDescriptions}
";
        }

        /// <summary>
        /// Scenario: Email user with link to confirm ownership of package with pending 'Microsoft' (propagating) owner.
        /// </summary>
        public static string NoticeOfPoliciesRequiredByPendingCoOwners(IEnumerable<string> usernames)
        {
            var prefix = GetPropagatorPrefix(usernames, "Pending owner");
            return $@"Note: {prefix} the following policies that will be enforced on your account once owner requests are accepted. If you are unsure and/or need more information, please contact support@nuget.org.
{PolicyDescriptions}
";
        }

        /* Policy notice in the email confirming new ownership sent to other package owners. */

        /// <summary>
        /// Scenario: Email existing package owners that 'Microsoft' (propagating) has been added as an owner.
        /// </summary>
        public static string NoticeOfPoliciesSubscribedByNewCoOwner(User newOwner)
        {
            return $@"User '{newOwner.Username}' has the following requirements that are now enforced for your account:
{PolicyDescriptions}

For more information, please contact support@nuget.org.";
        }

        /// <summary>
        /// Scenario: Email existing package owners that 'Microsoft' (propagating) has been added as an owner.
        /// </summary>
        public static string NoticeOfPoliciesSubscribedByPolicyOwner(IEnumerable<string> usernames, User newOwner)
        {
            var prefix = GetPropagatorPrefix(usernames, "Owner");
            return $@"{prefix} the following requirements that are now enforced for user '{newOwner.Username}':
{PolicyDescriptions}

For more information, please contact support@nuget.org.";
        }

        private static string GetPropagatorPrefix(IEnumerable<string> usernames, string noun)
        {
            var propagators = string.Join(", ", usernames);
            return usernames.Count() == 1 ?
                $"{noun} '{propagators}' has" :
                $"{noun}s '{propagators}' have";
        }
    }
}