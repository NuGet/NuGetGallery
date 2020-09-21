//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

using System.Web;

namespace NuGet.Shim.CookieCompliance
{
    /// <summary>
    /// Cookie expiration service, used to expire cookies.
    /// </summary>
    interface ICookieExpirationService
    {
        /// <summary>
        /// The function is used to expire analytics cookies.
        /// </summary>
        /// <param name="httpContext">The httpContext.</param>
        void ExpireAnalyticsCookies(HttpContextBase httpContext);

        /// <summary>
        /// The function is used to expire social media cookies.
        /// </summary>
        /// <param name="httpContext">The httpContext.</param>
        void ExpireSocialMediaCookies(HttpContextBase httpContext);

        /// <summary>
        /// The function is used to expire advertising cookies.
        /// </summary>
        /// <param name="httpContext">The httpContext.</param>
        void ExpireAdvertisingCookies(HttpContextBase httpContext);
    }
}