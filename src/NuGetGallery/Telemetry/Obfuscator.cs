// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    internal static class Obfuscator
    {
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        internal const string DefaultTelemetryUserName = "ObfuscatedUserName";

        internal static readonly HashSet<string> ObfuscatedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Organizations/Account",
            "Organizations/ChangeEmailSubscription",
            "Packages/ConfirmPendingOwnershipRequest",
            "Packages/RejectPendingOwnershipRequest",
            "Packages/CancelPendingOwnershipRequest",
            "Users/Confirm",
            "Users/ConfirmTransform",
            "Users/Delete",
            "Users/Profiles",
            "Users/ResetPassword"};

        internal static string DefaultObfuscatedUrl(Uri url)
        {
            return url == null ? string.Empty : $"{url.Scheme}://{url.Host}/{DefaultTelemetryUserName}";
        }
    }
}