// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Cookie compliance service, used to comply with EU privacy laws.
    /// </summary>
    public interface ICookieComplianceService
    {
        /// <summary>
        /// Run service startup initialization, on App_Start.
        /// </summary>
        Task InitializeAsync(string siteName, IDiagnosticsService diagnostics);

        /// <summary>
        /// Determine if consent is still needed for writing non-essential cookies.
        /// </summary>
        /// <returns>True if consent is needed, false if consent is already provided or not required.</returns>
        bool NeedsConsentForNonEssentialCookies(HttpRequestBase request);

        /// <summary>
        /// Determine if non-essential cookies can be written.
        /// </summary>
        /// <returns>True if non-essential cookies can be written, false otherwise.</returns>
        bool CanWriteNonEssentialCookies(HttpRequestBase request);

        /// <summary>
        /// Get HTML markup for the cookie consent banner.
        /// </summary>
        string GetConsentMarkup();

        /// <summary>
        /// Get CSS links for the cookie consent banner.
        /// </summary>
        string[] GetConsentStylesheets();

        /// <summary>
        /// Get Javascript links for the cookie consent banner.
        /// </summary>
        string[] GetConsentScripts();
    }
}
