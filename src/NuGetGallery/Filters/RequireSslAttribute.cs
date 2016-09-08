﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery.Filters
{
    // This code is identical to System.Web.Mvc except that we allow for working in localhost environment without https and we force authenticated users to use SSL
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class RequireSslAttribute : FilterAttribute, IAuthorizationFilter
    {
        public IGalleryConfigurationService ConfigService { get; set; }

        public async void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            var request = filterContext.HttpContext.Request;
            if ((await ConfigService.GetCurrent()).RequireSSL && !request.IsSecureConnection)
            {
                HandleNonHttpsRequest(filterContext);
            }
        }

        private async void HandleNonHttpsRequest(AuthorizationContext filterContext)
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

                var sslPort = (await ConfigService.GetCurrent()).SSLPort;
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