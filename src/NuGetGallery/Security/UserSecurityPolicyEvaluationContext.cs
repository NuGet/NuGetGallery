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
        private Lazy<HttpContextBase> _httpContext;
        private User _sourceAccount;
        private User _targetAccount;

        /// <summary>
        /// Current http context.
        /// </summary>
        public HttpContextBase HttpContext
        {
            get
            {
                return _httpContext.Value;
            }
        }

        /// <summary>
        /// Account under policy evaluation.
        /// </summary>
        public User CurrentUser
        {
            get
            {
                return HttpContext.GetCurrentUser();
            }
        }

        /// <summary>
        /// Account under policy evaluation.
        /// </summary>
        public User SourceAccount
        {
            get
            {
                return _sourceAccount ?? CurrentUser;
            }
        }

        /// <summary>
        /// Account under policy evaluation.
        /// </summary>
        public User TargetAccount
        {
            get
            {
                return _targetAccount ?? CurrentUser;
            }
        }

        /// <summary>
        /// Security policy entity.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies { get; }

        public UserSecurityPolicyEvaluationContext(IEnumerable<UserSecurityPolicy> policies, HttpContextBase httpContext)
        {
            Policies = policies ?? throw new ArgumentNullException(nameof(policies));

            _httpContext = new Lazy<HttpContextBase>(() => httpContext ?? new HttpContextWrapper(System.Web.HttpContext.Current));
        }

        public UserSecurityPolicyEvaluationContext(IEnumerable<UserSecurityPolicy> policies, User sourceAccount, User targetAccount, HttpContextBase httpContext = null)
            : this(policies, httpContext)
        {
            _sourceAccount = sourceAccount;
            _targetAccount = targetAccount;
        }
    }
}