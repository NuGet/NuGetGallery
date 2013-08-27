using System;
using System.Web.Mvc;

namespace NuGetGallery
{
    public static class RedirectHelper
    {
        public static string SafeRedirectUrl(UrlHelper url, string returnUrl)
        {
            if (!String.IsNullOrWhiteSpace(returnUrl)
                && url.IsLocalUrl(returnUrl)
                && returnUrl.Length > 1
                && returnUrl.StartsWith("/", StringComparison.Ordinal)
                && !returnUrl.StartsWith("//", StringComparison.Ordinal)
                && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
            {
                return returnUrl;
            }

            return url.Home();
        }
    }
}