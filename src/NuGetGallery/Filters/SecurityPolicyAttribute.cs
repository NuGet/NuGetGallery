// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Security;

namespace NuGetGallery.Filters
{
    /// <summary>
    /// Attribute to indicate that security policies should be evaluated for a controller action.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SecurityPolicyAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// Policy action to be evaluated.
        /// </summary>
        public SecurityPolicyAction Action { get; private set; }

        /// <summary>
        /// Result of the policy evaluation.
        /// </summary>
        public SecurityPolicyResult Result { get; private set; }

        /// <summary>
        /// Security policy service.
        /// </summary>
        public ISecurityPolicyService SecurityPolicyService { get; private set; }

        public SecurityPolicyAttribute(SecurityPolicyAction action)
        {
            Action = action;
        }
        
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            SecurityPolicyService = ((AppController)filterContext.Controller)?.GetService<ISecurityPolicyService>();

            base.OnAuthorization(filterContext);
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            Result = SecurityPolicyService.Evaluate(Action, httpContext);
            return Result.Success && base.AuthorizeCore(httpContext);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var owinContext = filterContext.HttpContext.GetOwinContext();
            owinContext.Response.StatusCode = 400;
            filterContext.Result = new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, Result.ErrorMessage);
        }
    }
}