// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Authentication;
using NuGetGallery.Configuration;

using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Areas.Admin.Filters
{
    /// <summary>
    /// MVC authorization filter for Admin API endpoints. Works in tandem with
    /// <see cref="AdminApiBearerAuthenticationHandler"/>, which runs earlier in the
    /// OWIN pipeline and performs the actual JWT validation asynchronously.
    ///
    /// This filter handles:
    /// - Returning 404 when the Admin API feature is disabled
    /// - Registering an OWIN auth challenge to prevent cookie auth redirects
    /// - Verifying that the handler successfully authenticated the caller
    /// - Storing the caller identity in HttpContext.Items for the controller
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AdminApiAuthAttribute : FilterAttribute, IAuthorizationFilter
    {
        internal static readonly string CallerIdentityItemKey = "AdminApi.CallerIdentity";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var configService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();
            if (!configService.Current.AdminApiEnabled)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.NotFound);
                return;
            }

            var owinContext = filterContext.HttpContext.GetOwinContext();

            // Register an OWIN auth challenge to prevent the cookie auth middleware
            // from intercepting 401 responses and converting them to 302 login redirects.
            owinContext.Authentication.Challenge(AdminApiBearerAuthenticationOptions.DefaultAuthenticationType);

            // Read the caller identity claim set by the OWIN auth handler.
            var callerIdentity = owinContext.Authentication.User?.FindFirst(AdminApiBearerAuthenticationHandler.CallerIdentityClaim)?.Value;

            if (!string.IsNullOrEmpty(callerIdentity))
            {
                filterContext.HttpContext.Items[CallerIdentityItemKey] = callerIdentity;
                return;
            }

            // Handler did not authenticate the request. Check for a specific error.
            owinContext.Environment.TryGetValue(AdminApiBearerAuthenticationOptions.AuthErrorEnvironmentKey, out var errorObj);

            if (errorObj is AdminApiBearerAuthenticationHandler.AuthError authError)
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(authError.StatusCode, authError.Message);
            }
            else
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Unauthorized,
                    "A valid Bearer token must be provided in the Authorization header.");
            }
        }
    }
}
