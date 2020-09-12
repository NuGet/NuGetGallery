// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Threading.Tasks;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Base cookie compliance service with access to some Gallery resources.
    /// </summary>
    public abstract class CookieComplianceServiceBase : ICookieComplianceService
    {
        public abstract Task<bool> CanWriteAnalyticsCookies(HttpRequestBase request);

        public abstract Task<bool> CanWriteSocialMediaCookies(HttpRequestBase request);

        public abstract Task<bool> CanWriteAdvertisingCookies(HttpRequestBase request);

        public abstract void ExpireAnalyticsCookies(HttpContextBase httpContextBase);

        public abstract void ExpireSocialMediaCookies(HttpContextBase httpContextBase);

        public abstract void ExpireAdvertisingCookies(HttpContextBase httpContextBase);

    }
}
