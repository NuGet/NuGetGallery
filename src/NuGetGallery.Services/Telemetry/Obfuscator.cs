// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery.Services.Telemetry
{
    public static class Obfuscator
    {
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        public const string DefaultTelemetryUserName = "ObfuscatedUserName";
        public const string DefaultTelemetryReturnUrl = "ObfuscatedReturnUrl";
        public const string DefaultTelemetryToken = "ObfuscatedToken";

        public static readonly HashSet<string> ObfuscatedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Organizations/AddMember",
            "Organizations/AddCertificate",
            "Organizations/CancelMemberRequest",
            "Organizations/ChangeEmailSubscription",
            "Organizations/ConfirmMemberRequest",
            "Organizations/DeleteCertificate",
            "Organizations/DeleteMember",
            "Organizations/GetCertificate",
            "Organizations/GetCertificates",
            "Organizations/ManageOrganization",
            "Organizations/RejectMemberRequest",
            "Organizations/UpdateMember",
            "Packages/CancelPendingOwnershipRequest",
            "Packages/ConfirmPendingOwnershipRequest",
            "Packages/RejectPendingOwnershipRequest",
            "Packages/SetRequiredSigner",
            "Users/Confirm",
            "Users/ConfirmTransform",
            "Users/Delete",
            "Users/Profiles",
            "Users/RejectTransform",
            "Users/ResetPassword",
        };

        public static string DefaultObfuscatedUrl(Uri url)
        {
            return url == null ? string.Empty : $"{url.Scheme}://{url.Host}/{DefaultTelemetryUserName}";
        }
    }
}