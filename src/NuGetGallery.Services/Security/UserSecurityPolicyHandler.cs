// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services.Security
{
    /// <summary>
    /// Policy handler that defines behavior for specific user policy types.
    /// </summary>
    public abstract class UserSecurityPolicyHandler 
        : SecurityPolicyHandler<UserSecurityPolicyEvaluationContext>
    {
        public UserSecurityPolicyHandler(string name, SecurityPolicyAction action)
            : base(name, action)
        {
        }
    }
}