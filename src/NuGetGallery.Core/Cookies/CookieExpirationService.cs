// Copyright (c) .NET Foundation. All rights reserved.
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
        private static readonly IReadOnlyList<string> GoogleAnalyticsCookies = new[]
        {
            "_ga",
            "_gid",
            "_gat",
        };

        // Application Insights cookies
        private static readonly IReadOnlyList<string> ApplicationInsightsCookies = new[]
        {
            "ai_user",
            "ai_session",
        };

        private readonly string Domain;
        private readonly string PrimaryDomain;

        public CookieExpirationService(string domain)
        {
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
            PrimaryDomain = GetPrimaryDomain(Domain);
        }

        public void ExpireAnalyticsCookies(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            GoogleAnalyticsCookies.ToList().ForEach(cookie => ExpireCookieByName(httpContext, PrimaryDomain));
            ApplicationInsightsCookies.ToList().ForEach(cookie => ExpireCookieByName(httpContext, cookie));
        }

        public void ExpireSocialMediaCookies(HttpContextBase httpContext) { }
        public void ExpireAdvertisingCookies(HttpContextBase httpContext) { }

        public void ExpireCookieByName(HttpContextBase httpContext, string cookieName, string domain = null)
        {
            var request = httpContext.Request;
            var response = httpContext.Response;
            if (request.Cookies[cookieName] != null)
            {
                response.Cookies[cookieName].Expires = CookieExpirationTime;
                response.Cookies[cookieName].Secure = false;

                if (domain != null)
                {
                    response.Cookies[cookieName].Domain = domain;
                }
            }
        }

        public string GetPrimaryDomain(string domain)
        {
            var index1 = domain.LastIndexOf('.');
            if (index1 < 0)
            {
                return domain;
            }

            var index2 = domain.LastIndexOf('.', index1 - 1);
            if (index2 < 0)
            {
                return domain;
            }

            return domain.Substring(index2 + 1);
        }
    }
}