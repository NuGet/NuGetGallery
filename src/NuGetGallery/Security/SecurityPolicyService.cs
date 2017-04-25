// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Service that looks up and evaluates security policies for attributed controller actions.
    /// </summary>
    public class SecurityPolicyService : ISecurityPolicyService
    {
        private static Lazy<IEnumerable<UserSecurityPolicyHandler>> _userPolicyHandlers =
            new Lazy<IEnumerable<UserSecurityPolicyHandler>>(CreateUserPolicyHandlers);

        protected virtual IEnumerable<UserSecurityPolicyHandler> UserPolicyHandlers
        {
            get
            {
                return _userPolicyHandlers.Value;
            }
        }

        /// <summary>
        /// Look up and evaluation of security policies for the specified action.
        /// </summary>
        public SecurityPolicyResult Evaluate(SecurityPolicyAction action, HttpContextBase httpContext)
        {
            var user = httpContext.GetCurrentUser();
            foreach (var handler in UserPolicyHandlers.Where(h => h.Action == action))
            {
                var foundPolicies = user.SecurityPolicies.Where(p => p.Name.Equals(handler.Name, StringComparison.OrdinalIgnoreCase));
                if (foundPolicies.Any())
                {
                    var result = handler.Evaluate(new UserSecurityPolicyContext(httpContext, foundPolicies));
                    if (!result.Success)
                    {
                        return result;
                    }
                }
            }
            return SecurityPolicyResult.SuccessResult;
        }

        /// <summary>
        /// Create any supported policy handlers.
        /// </summary>
        private static IEnumerable<UserSecurityPolicyHandler> CreateUserPolicyHandlers()
        {
            yield return new RequireMinClientVersionForPushPolicy();
            yield return new RequirePackageVerifyScopePolicy();
        }       
    }
}