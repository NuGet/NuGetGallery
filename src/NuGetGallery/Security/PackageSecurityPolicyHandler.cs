// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services.Security;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Policy handler that defines behavior for specific user policy types requiring package policy evaluation.
    /// </summary>
    public abstract class PackageSecurityPolicyHandler : SecurityPolicyHandler<PackageSecurityPolicyEvaluationContext>
    {
        public PackageSecurityPolicyHandler(string name, SecurityPolicyAction action) 
            : base(name, action)
        {
        }
    }
}