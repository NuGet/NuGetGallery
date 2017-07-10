// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Base cookie compliance service with access to some Gallery resources.
    /// </summary>
    public abstract class CookieComplianceServiceBase : ICookieComplianceService
    {
        private string _siteName;
        private IDiagnosticsSource _diagnostics;

        protected string SiteName
        {
            get
            {
                return _siteName ?? throw new InvalidOperationException("Cookie compliance service has not been initialized");
            }
        }

        protected IDiagnosticsSource Diagnostics
        {
            get
            {
                return _diagnostics ?? throw new InvalidOperationException("Cookie compliance service has not been initialized");
            }
        }

        protected string Locale
        {
            get
            {
                return CultureInfo.CurrentCulture.Name;
            }
        }

        public virtual Task InitializeAsync(string siteName, IDiagnosticsService diagnostics)
        {
            _siteName = siteName;
            _diagnostics = diagnostics.GetSource("CookieComplianceService");
            return Task.Delay(0);
        }
        
        public abstract bool CanWriteNonEssentialCookies(HttpRequestBase request);

        public abstract bool NeedsConsentForNonEssentialCookies(HttpRequestBase request);

        public abstract string GetConsentMarkup();

        public abstract string[] GetConsentScripts();

        public abstract string[] GetConsentStylesheets();
    }
}
