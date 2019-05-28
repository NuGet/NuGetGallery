// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.Security
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
        /// Current http context. This has been required for some user security policies in order
        /// to get the current user and/or current request details.
        /// </summary>
        public HttpContextBase HttpContext
        {
            get
            {
                return _httpContext.Value;
            }
        }

        /// <summary>
        /// Current user.
        /// </summary>
        public User CurrentUser
        {
            get
            {
                return HttpContext.GetCurrentUser();
            }
        }

        /// <summary>
        /// Account where the security policy came from.
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
        /// Security policies to be evaluated.
        /// </summary>
        public IEnumerable<UserSecurityPolicy> Policies { get; }

        /// <summary>
        /// Create a policy (user) context, which uses the httpContext.
        /// </summary>
        public UserSecurityPolicyEvaluationContext(
            IEnumerable<UserSecurityPolicy> policies,
            HttpContextBase httpContext)
        {
            Policies = policies ?? throw new ArgumentNullException(nameof(policies));

            _httpContext = new Lazy<HttpContextBase>(() => httpContext
                ?? new HttpContextWrapper(System.Web.HttpContext.Current));
        }

        /// <summary>
        /// Create a policy (organization) context, which requires the source (organization) and target (member) accounts for context.
        /// </summary>
        public UserSecurityPolicyEvaluationContext(
            IEnumerable<UserSecurityPolicy> policies,
            User sourceAccount,
            User targetAccount,
            HttpContextBase httpContext = null)
            : this(policies, httpContext)
        {
            _sourceAccount = sourceAccount;
            _targetAccount = targetAccount;
        }
    }
}