// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Default, no-op instance of the cookie compliance service, used when no shim is registered.
    /// </summary>
    public class NullCookieComplianceService: CookieComplianceServiceBase
    {
        private static readonly string[] EmptyStringArray = new string[0];

        // Consent is not necessary and cookies can be written.

        public override bool NeedsConsentForNonEssentialCookies(HttpRequestBase request) => false;

        public override bool CanWriteNonEssentialCookies(HttpRequestBase request) => true;

        // No markdown or scripts will be included.

        public override CookieConsentMessage GetConsentMessage(HttpRequestBase request, string locale = null) => null;

        public override string GetConsentMarkup(HttpRequestBase request, string locale = null) => string.Empty;

        public override IEnumerable<string> GetConsentScripts(HttpRequestBase request, string locale = null) => EmptyStringArray;

        public override IEnumerable<string> GetConsentStylesheets(HttpRequestBase request, string locale = null) => EmptyStringArray;

        public override Task<bool> CanWriteAnalyticsCookies(HttpRequestBase request) => Task.FromResult(false);

        public override Task<bool> CanWriteSocialMediaCookies(HttpRequestBase request) => Task.FromResult(false);

        public override Task<bool> CanWriteAdvertisingCookies(HttpRequestBase request) => Task.FromResult(false);
    }
}
