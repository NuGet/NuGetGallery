// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery.Authentication;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy to require scoped API keys with verify scope on package verification callback.
    /// </summary>
    public class RequirePackageVerifyScopePolicy : UserSecurityPolicyHandler
    {
        public const string PolicyName = nameof(RequirePackageVerifyScopePolicy);

        public RequirePackageVerifyScopePolicy()
            : base(PolicyName, SecurityPolicyAction.PackageVerify)
        {
        }

        public override Task<SecurityPolicyResult> EvaluateAsync(UserSecurityPolicyEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var identity = context.HttpContext.User.Identity;
            if (identity.HasExplicitScopeAction(NuGetScopes.PackageVerify))
            {
                return Task.FromResult(SecurityPolicyResult.SuccessResult);
            }

            return Task.FromResult(SecurityPolicyResult.CreateErrorResult(Strings.SecurityPolicy_RequireApiKeyWithPackageVerifyScope));
        }
    }
}