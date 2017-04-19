// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Extensions on <see cref="HttpRequest"/> and <see cref="HttpRequestBase"/>
    /// </summary>
    public static class HttpRequestExtensions
    {
        public static string GetNuGetClientVersion(this HttpRequest request)
        {
            return GetNuGetClientVersion(new HttpRequestWrapper(request));
        }

        private static string GetNuGetClientVersion(this HttpRequestBase request)
        {
            return request?.Headers[Constants.ClientVersionHeaderName];
        }

        public static bool IsNuGetClientVersionOrHigher(this HttpRequestBase request, Version requiredVersion)
        {
            var versionString = request.GetNuGetClientVersion();
            if (!string.IsNullOrWhiteSpace(versionString))
            {
                Version version;
                return Version.TryParse(versionString, out version) &&
                    version >= requiredVersion;
            }
            return false;
        }

        /// <summary>
        /// Retrieve culture of client. 
        /// </summary>
        /// <param name="request">Current request.</param>
        /// <returns><c>null</c> if not to be determined.</returns>
        public static CultureInfo DetermineClientCulture(this HttpRequest request)
        {
            return DetermineClientCulture(new HttpRequestWrapper(request));
        }
        
        /// <summary>
        /// Retrieve culture of client. 
        /// </summary>
        /// <param name="request">Current request.</param>
        /// <returns><c>null</c> if not to be determined.</returns>
        public static CultureInfo DetermineClientCulture(this HttpRequestBase request)
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