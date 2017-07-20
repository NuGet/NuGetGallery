// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
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
        private string _domain;
        private IDiagnosticsSource _diagnostics;

        protected internal string Domain => _domain ?? throw new InvalidOperationException(CoreStrings.CookieComplianceServiceNotInitialized);

        protected internal IDiagnosticsSource Diagnostics => _diagnostics ?? throw new InvalidOperationException(CoreStrings.CookieComplianceServiceNotInitialized);

        public virtual Task InitializeAsync(string domain, IDiagnosticsService diagnostics, CancellationToken cancellationToken)
        {
            // Service should only be initialized once.
            if (_domain != null)
            {
                throw new InvalidOperationException(CoreStrings.CookieComplianceServiceAlreadyInitialized);
            }

            _domain = domain;
            _diagnostics = diagnostics.GetSource(GetType().Name);

            return Task.Delay(0);
        }
        
        public abstract bool CanWriteNonEssentialCookies(HttpContextBase httpContext);

        public abstract bool NeedsConsentForNonEssentialCookies(HttpContextBase httpContext);

        public abstract string GetConsentMarkup(HttpContextBase httpContext, string locale = null);

        public abstract IEnumerable<string> GetConsentScripts(HttpContextBase httpContext, string locale = null);

        public abstract IEnumerable<string> GetConsentStylesheets(HttpContextBase httpContext, string locale = null);
    }
}
