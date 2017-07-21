// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Cookie compliance service, used to comply with EU privacy laws.
    /// </summary>
    public interface ICookieComplianceService
    {
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
        /// Get the cookie consent banner message and resources. This API is an alternative to the default
        /// rendering APIs below and can be used to customize the UI. Note that the messaging must remain intact.
        /// </summary>
        CookieConsentMessage GetConsentMessage(HttpRequestBase request, string locale = null);

        #region Default CookieConsent rendering

        /// <summary>
        /// Get the default HTML markup for the cookie consent banner.
        /// </summary>
        string GetConsentMarkup(HttpRequestBase request, string locale = null);

        /// <summary>
        /// Get the default CSS links for the cookie consent banner.
        /// </summary>
        IEnumerable<string> GetConsentStylesheets(HttpRequestBase request, string locale = null);

        /// <summary>
        /// Get the default Javascript links for the cookie consent banner.
        /// </summary>
        IEnumerable<string> GetConsentScripts(HttpRequestBase request, string locale = null);

        #endregion
    }
}
