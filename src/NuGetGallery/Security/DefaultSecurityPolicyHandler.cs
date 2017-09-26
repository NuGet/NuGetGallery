// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery.Security
{
    /// <summary>
    /// A wrapper around <see cref="UserSecurityPolicyHandler"/> that provides the ability to set a default policy provided in ctor.
    /// </summary>
    public class DefaultSecurityPolicyHandler
    {
        private UserSecurityPolicyHandler _handler;
        private IEnumerable<UserSecurityPolicy> _securityPolicies;


        public DefaultSecurityPolicyHandler(UserSecurityPolicyHandler handler, IEnumerable<UserSecurityPolicy> securityPolicies)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _securityPolicies = securityPolicies ?? throw new ArgumentNullException(nameof(securityPolicies));
        }

        public SecurityPolicyResult Evaluate(SecurityPolicyEvaluationContext context)
        {
            var userContext = new UserSecurityPolicyEvaluationContext(context, _securityPolicies);
            return _handler.Evaluate(userContext);
        }
    }
}