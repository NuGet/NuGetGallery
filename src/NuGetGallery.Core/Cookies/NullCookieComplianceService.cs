// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Threading.Tasks;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Default, no-op instance of the cookie compliance service, used when no shim is registered.
    /// </summary>
    public class NullCookieComplianceService : CookieComplianceServiceBase
    {
        public override Task<bool> CanWriteAnalyticsCookies(HttpRequestBase request) => Task.FromResult(false);

        public override Task<bool> CanWriteSocialMediaCookies(HttpRequestBase request) => Task.FromResult(false);

        public override Task<bool> CanWriteAdvertisingCookies(HttpRequestBase request) => Task.FromResult(false);

        public override void ExpireAnalyticsCookies(HttpContextBase httpContextBase) { }

        public override void ExpireSocialMediaCookies(HttpContextBase httpContextBase) { }

        public override void ExpireAdvertisingCookies(HttpContextBase httpContextBase) { }
    }
}
