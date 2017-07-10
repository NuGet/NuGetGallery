// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Default instance of the cookie compliance service, used when no shim is registered.
    /// </summary>
    public class NullCookieComplianceService: ICookieComplianceService
    {
        public virtual Task InitializeAsync(string siteName, IDiagnosticsService diagnostics)
        {
            return Task.Delay(0);
        }
        
        public bool NeedsConsentForNonEssentialCookies(HttpRequestBase request)
        {
            return false;
        }

        public bool CanWriteNonEssentialCookies(HttpRequestBase request)
        {
            return true;
        }

        public string GetConsentMarkup()
        {
            return string.Empty;
        }

        public string[] GetConsentScripts()
        {
            return new string[0];
        }

        public string[] GetConsentStylesheets()
        {
            return new string[0];
        }
    }
}
