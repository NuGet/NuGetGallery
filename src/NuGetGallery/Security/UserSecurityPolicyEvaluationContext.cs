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
    public class UserSecurityPolicyEvaluationContext
    {
        /// <summary>
        /// Current http context.
        /// </summary>
        public HttpContextBase HttpContext { get; }

        /// <summary>
        /// Security policy entity.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public UserSecurityPolicyEvaluationContext(HttpContextBase httpContext, IEnumerable<UserSecurityPolicy> policies)
        {
            HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }
    }
}