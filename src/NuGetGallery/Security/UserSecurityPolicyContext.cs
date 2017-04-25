// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Context providing user security policy handlers with resources necessary for policy actions.
    /// </summary>
    public class UserSecurityPolicyContext
    {
        public UserSecurityPolicyContext(HttpContextBase httpContext, IEnumerable<UserSecurityPolicy> policies)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }
            if (policies == null)
            {
                throw new ArgumentNullException(nameof(policies));
            }

            HttpContext = httpContext;
            Policies = policies;
        }

        /// <summary>
        /// Current http context.
        /// </summary>
        public HttpContextBase HttpContext { get; private set; }

        /// <summary>
        /// Security policy entity.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies { get; private set; }

    }
}