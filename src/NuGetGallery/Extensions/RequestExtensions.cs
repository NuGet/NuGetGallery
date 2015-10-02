using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Extensions on <see cref="HttpRequest"/> and <see cref="HttpRequestBase"/>
    /// </summary>
    public static class RequestExtensions
    {
        /// <summary>
        /// Retrieve culture of client. 
        /// </summary>
        /// <param name="request">Current request.</param>
        /// <returns><c>null</c> if not to be determined.</returns>
        public static CultureInfo DetermineClientLocale(this HttpRequest request)
        {
            return DetermineClientLocale(new HttpRequestWrapper(request));
        }
        
        /// <summary>
        /// Retrieve culture of client. 
        /// </summary>
        /// <param name="request">Current request.</param>
        /// <returns><c>null</c> if not to be determined.</returns>
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

            //first try parse of full langcodes. Stop with first success.
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

            //try parse again with first 2 chars.  Stop with first success.
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