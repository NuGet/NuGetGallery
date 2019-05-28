// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using NuGetGallery.Services.Security;

namespace NuGetGallery.Security
{
    /// <summary>
    /// A policy which enables subscribing package owners to retain control
    /// from non-subscribing package owners of changing the required signer
    /// for a package registration.
    /// </summary>
    public sealed class ControlRequiredSignerPolicy : RequiredSignerPolicy
    {
        public const string PolicyName = nameof(ControlRequiredSignerPolicy);

        public ControlRequiredSignerPolicy()
            : base(PolicyName, SecurityPolicyAction.ControlRequiredSigner)
        {
        }
    }
}