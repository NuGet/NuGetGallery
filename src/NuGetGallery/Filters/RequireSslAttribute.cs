// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace NuGetGallery.Filters
{
    // This code is identical to System.Web.Mvc except that we allow for working in localhost environment without https and we force authenticated users to use SSL
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class RequireSslAttribute : FilterAttribute, IAuthorizationFilter
    {
        public IGalleryConfigurationService ConfigService { get; set; }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            var request = filterContext.HttpContext.Request;
            // Disabled warning because IAuthorizationFilter must be synchronous.
#pragma warning disable CS0618 // Type or member is obsolete
            if (ConfigService.Current.RequireSSL && !request.IsSecureConnection)
            {
                HandleNonHttpsRequest(filterContext);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private void HandleNonHttpsRequest(AuthorizationContext filterContext)
        {
            // only redirect for GET requests, otherwise the browser might not propagate the verb and request
            // body correctly.
            if (!String.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.SSLRequired);
            }
            else
            {
                // redirect to HTTPS version of page
                var portString = String.Empty;

                // Disabled warning because IAuthorizationFilter must be synchronous.
#pragma warning disable CS0618 // Type or member is obsolete
                var sslPort = ConfigService.Current.SSLPort;
#pragma warning restore CS0618 // Type or member is obsolete
                if (sslPort != 443)
                {
                    portString = String.Format(CultureInfo.InvariantCulture, ":{0}", sslPort);
                }

                string url = "https://" + filterContext.HttpContext.Request.Url.Host + portString + filterContext.HttpContext.Request.RawUrl;
                filterContext.Result = new RedirectResult(url);
            }
        }
    }
}