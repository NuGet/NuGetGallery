using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Helpers;

namespace NuGetGallery.Infrastructure
{
    public static class AntiForgeryExtensions
    {
        public static string GetAntiForgeryCookie(this HttpRequestBase self)
        {
            if (self == null)
            {
                throw new ArgumentNullException("self");
            }

            HttpCookie cookie = self.Cookies[AntiForgeryConfig.CookieName];
            if (cookie == null)
            {
                return null;
            }
            return cookie.Value;
        }

        public static void SetAntiForgeryCookie(this HttpResponseBase self, string value)
        {
            if (self == null)
            {
                throw new ArgumentNullException("self");
            }
            if (String.IsNullOrEmpty(value))
            {
                throw new ArgumentException("'value' must be a non-empty string", "value");
            }

            HttpCookie cookie = new HttpCookie(AntiForgeryConfig.CookieName, value)
            {
                HttpOnly = true
            };
            self.Cookies.Set(cookie);
        }
    }
}