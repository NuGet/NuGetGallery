// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Service that manages evaluation of security policies.
    /// </summary>
    public interface ISecurityPolicyService
    {
        /// <summary>
        /// Evaluate any security policies that may apply to the current context.
        /// </summary>
        /// <param name="action">Security policy action.</param>
        /// <param name="context">Authorization context.</param>
        /// <returns>Policy result indicating success or failure.</returns>
        SecurityPolicyResult Evaluate(SecurityPolicyAction action, HttpContextBase httpContext);
    }
}