// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy to require scoped API keys with verify scope on package verification callback.
    /// </summary>
    public class RequirePackageVerifyScopePolicy : UserSecurityPolicyHandler
    {
        public RequirePackageVerifyScopePolicy()
            : base(nameof(RequirePackageVerifyScopePolicy), SecurityPolicyAction.PackageVerify)
        {
        }

        public override SecurityPolicyResult Evaluate(UserSecurityPolicyContext context)
        {
            var identity = context.HttpContext.User.Identity;
            if (identity.HasPackageVerifyScopeClaim())
            {
                return SecurityPolicyResult.SuccessResult;
            }

            return new SecurityPolicyResult(false, Strings.SecurityPolicy_RequireApiKeyWithPackageVerifyScope);
        }
    }
}