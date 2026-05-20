// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery.Filters
{
    /// <summary>
    /// MVC authorization filter for Admin API endpoints. Works in tandem with
    /// <see cref="AdminApiBearerAuthMiddleware"/>, which runs earlier in the OWIN
    /// pipeline and performs the actual JWT validation asynchronously.
    ///
    /// This filter handles:
    /// - Returning 404 when the Admin API feature is disabled
    /// - Registering an OWIN auth challenge to prevent cookie auth redirects
    /// - Verifying that the middleware successfully authenticated the caller
    /// - Storing the authorized party (azp) in HttpContext.Items for the controller
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AdminApiAuthAttribute : FilterAttribute, IAuthorizationFilter
    {
        internal static readonly string AzpItemKey = "AdminApi.AuthorizedParty";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var configService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();
            if (!configService.Current.AdminApiEnabled)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.NotFound);
                return;
            }

            // Register an explicit OWIN auth challenge for a type that no middleware handles.
            // This prevents the Active cookie auth middleware from intercepting 401 responses
            // and converting them to 302 login redirects. The cookie middleware only auto-redirects
            // when there are no explicit challenges registered.
            filterContext.HttpContext.GetOwinContext().Authentication.Challenge("AdminApi");

            // The OWIN middleware (AdminApiBearerAuthMiddleware) has already validated the
            // bearer token and stored the authorized party in the OWIN environment.
            // Read it from HttpContext.Items (which wraps the OWIN environment).
            var azp = filterContext.HttpContext.Items[AzpItemKey] as string;
            if (string.IsNullOrEmpty(azp))
            {
                // Middleware did not authenticate the request. It already wrote a
                // 401/403 response, but MVC may still try to execute the action if
                // the middleware didn't short-circuit (e.g., during unit tests).
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Unauthorized,
                    "A valid Bearer token must be provided in the Authorization header.");
                return;
            }
        }
    }
}
