// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Security
{
    /// <summary>
    /// A policy which enables subscribing package owners to automatically
    /// overwrite the current required signer for a package registration.
    /// </summary>
    public sealed class AutomaticallyOverwriteRequiredSignerPolicy : RequiredSignerPolicy
    {
        public const string PolicyName = nameof(AutomaticallyOverwriteRequiredSignerPolicy);

        public AutomaticallyOverwriteRequiredSignerPolicy()
            : base(PolicyName, SecurityPolicyAction.AutomaticallyOverwriteRequiredSigner)
        {
        }
    }
}