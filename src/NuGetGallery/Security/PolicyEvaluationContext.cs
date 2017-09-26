// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Context providing security policy handlers with resources necessary for policy evaluation.
    /// </summary>
    public class SecurityPolicyEvaluationContext
    {
        /// <summary>
        /// Current http context.
        /// </summary>
        public HttpContextBase HttpContext { get; }

        public SecurityPolicyEvaluationContext(HttpContextBase httpContext)
        {
            HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        }
    }
}