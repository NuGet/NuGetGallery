﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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

        public override SecurityPolicyResult Evaluate(UserSecurityPolicyEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var identity = context.HttpContext.User.Identity;
            if (identity.HasPackageVerifyScopeClaim())
            {
                return SecurityPolicyResult.SuccessResult;
            }

            return SecurityPolicyResult.CreateErrorResult(Strings.SecurityPolicy_RequireApiKeyWithPackageVerifyScope);
        }
    }
}