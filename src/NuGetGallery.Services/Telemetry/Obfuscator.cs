// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public static class Obfuscator
    {
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        public const string DefaultTelemetryReturnUrl = "ObfuscatedReturnUrl";
        public const string DefaultTelemetryToken = "ObfuscatedToken";

        public static readonly HashSet<string> ObfuscatedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Organizations/ConfirmMemberRequest",
            "Organizations/ConfirmMemberRequestRedirect",
            "Organizations/RejectMemberRequest",
            "Organizations/RejectMemberRequestRedirect",
            "Packages/CancelPendingOwnershipRequest",
            "Packages/ConfirmPendingOwnershipRequest",
            "Packages/ConfirmPendingOwnershipRequestRedirect",
            "Packages/RejectPendingOwnershipRequest",
            "Packages/RejectPendingOwnershipRequestRedirect",
            "Users/CancelTransformToOrganization",
            "Users/CancelTransformToOrganizationRedirect",
            "Users/Confirm",
            "Users/ConfirmTransformToOrganization",
            "Users/ConfirmTransformToOrganizationRedirect",
            "Users/RejectTransformToOrganization",
            "Users/RejectTransformToOrganizationRedirect",
            "Users/ResetPassword",
        };

        public static string DefaultObfuscatedUrl(Uri url)
        {
            return url == null ? string.Empty : $"{url.Scheme}://{url.Host}/ObfuscatedPath";
        }
    }
}
