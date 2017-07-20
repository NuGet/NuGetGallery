// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Default, no-op instance of the cookie compliance service, used when no shim is registered.
    /// </summary>
    public class NullCookieComplianceService: ICookieComplianceService
    {
        private static readonly string[] EmptyStringArray = new string[0];

        // No initialization.

        public virtual Task InitializeAsync(string domain, IDiagnosticsService diagnostics, CancellationToken cancellationToken) => Task.Delay(0);

        // Consent is not necessary and cookies can be written.

        public bool NeedsConsentForNonEssentialCookies(HttpContextBase httpContext) => false;

        public bool CanWriteNonEssentialCookies(HttpContextBase httpContext) => true;

        // No markdown or scripts will be included.

        public string GetConsentMarkup(HttpContextBase httpContext, string locale = null) => string.Empty;

        public IEnumerable<string> GetConsentScripts(HttpContextBase httpContext, string locale = null) => EmptyStringArray;

        public IEnumerable<string> GetConsentStylesheets(HttpContextBase httpContext, string locale = null) => EmptyStringArray;
    }
}
