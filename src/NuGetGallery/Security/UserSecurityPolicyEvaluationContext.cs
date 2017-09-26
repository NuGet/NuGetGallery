// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Context providing user security policy handlers with resources necessary for policy evaluation.
    /// </summary>
    public class UserSecurityPolicyEvaluationContext : SecurityPolicyEvaluationContext
    {
        /// <summary>
        /// Security policy entity.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public UserSecurityPolicyEvaluationContext(SecurityPolicyEvaluationContext context, IEnumerable<UserSecurityPolicy> policies) : this(context.HttpContext, policies)
        {
        }

        public UserSecurityPolicyEvaluationContext(HttpContextBase httpContext, IEnumerable<UserSecurityPolicy> policies) : base(httpContext)
        {
            Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }
    }
}