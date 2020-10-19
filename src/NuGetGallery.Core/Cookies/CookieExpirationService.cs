﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Linq;
using System.Collections.Generic;

namespace NuGetGallery.Cookies
{
    public class CookieExpirationService : ICookieExpirationService
    {
        private static readonly DateTime CookieExpirationTime = new DateTime(2010, 1, 1);

        // Google Analytics cookies
        private static readonly string[] GoogleAnalyticsCookies = new[]
        {
            "_ga",
            "_gid",
            "_gat",
        };

        // Application Insights cookies
        private static readonly string[] ApplicationInsightsCookies = new[]
        {
            "ai_user",
            "ai_session",
        };

        public void ExpireAnalyticsCookies(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            Array.ForEach(GoogleAnalyticsCookies, cookieName => ExpireCookieByName(httpContext, cookieName));
            Array.ForEach(ApplicationInsightsCookies, cookieName => ExpireCookieByName(httpContext, cookieName));
        }

        public void ExpireSocialMediaCookies(HttpContextBase httpContext) { }
        public void ExpireAdvertisingCookies(HttpContextBase httpContext) { }

        public void ExpireCookieByName(HttpContextBase httpContext, string cookieName, string domain = null)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (string.IsNullOrEmpty(cookieName))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(cookieName));
            }

            var request = httpContext.Request;
            var response = httpContext.Response;
            if (request == null || response == null || request.Cookies == null || response.Cookies == null)
            {
                return;
            }

            if (request.Cookies[cookieName] != null)
            {
                response.Cookies[cookieName].Expires = CookieExpirationTime;

                if (domain != null)
                {
                    response.Cookies[cookieName].Domain = domain;
                }
            }
        }
    }
}