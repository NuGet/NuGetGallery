// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Threading.Tasks;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Default, no-op instance of the cookie compliance service, used when no shim is registered.
    /// </summary>
    public class NullCookieComplianceService : ICookieComplianceService
    {
        public Task<bool> CanWriteAnalyticsCookiesAsync(HttpRequestBase request) => Task.FromResult(false);

        public Task<bool> CanWriteSocialMediaCookiesAsync(HttpRequestBase request) => Task.FromResult(false);

        public Task<bool> CanWriteAdvertisingCookiesAsync(HttpRequestBase request) => Task.FromResult(false);
    }
}