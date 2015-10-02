using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public static class RequestExtensions
    {
        public static CultureInfo DetermineClientLocale(this HttpRequest request)
        {
            return DetermineClientLocale(new HttpRequestWrapper(request));
        }

        public static CultureInfo DetermineClientLocale(this HttpRequestBase request)
        {
            if (request == null)
            {
                return null;
            }

            string[] languages = request.UserLanguages;
            if (languages == null)
            {
                return null;
            }

            foreach (string language in languages)
            {
                string lang = language.ToLowerInvariant().Trim();
                try
                {
                    return CultureInfo.GetCultureInfo(lang);
                }
                catch (CultureNotFoundException)
                {
                }
            }

            foreach (string language in languages)
            {
                string lang = language.ToLowerInvariant().Trim();
                if (lang.Length > 2)
                {
                    string lang2 = lang.Substring(0, 2);
                    try
                    {
                        return CultureInfo.GetCultureInfo(lang2);
                    }
                    catch (CultureNotFoundException)
                    {
                    }
                }
            }

            return null;
        }
    }
}