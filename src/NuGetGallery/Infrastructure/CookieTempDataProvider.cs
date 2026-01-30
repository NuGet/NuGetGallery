// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class CookieTempDataProvider : ITempDataProvider
    {
        private const string TempDataCookieKey = "__Controller::TempData";
        private readonly HttpContextBase _httpContext;

        // Methods
        public CookieTempDataProvider(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            _httpContext = httpContext;
        }

        private bool CookieHasTempData
        {
            get
            {
                return ((_httpContext.Response != null) && (_httpContext.Response.Cookies != null)) &&
                       (_httpContext.Response.Cookies[TempDataCookieKey] != null);
            }
        }

        public HttpContextBase HttpContext
        {
            get { return _httpContext; }
        }

        IDictionary<string, object> ITempDataProvider.LoadTempData(ControllerContext controllerContext)
        {
            return LoadTempData(controllerContext);
        }

        void ITempDataProvider.SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            SaveTempData(controllerContext, values);
        }

        protected virtual IDictionary<string, object> LoadTempData(ControllerContext controllerContext)
        {
            HttpCookie cookie;
            try
            {
                cookie = _httpContext.Request.Cookies[TempDataCookieKey];
            }
            catch (HttpRequestValidationException ex)
            {
                throw new HttpException(
                    (int)HttpStatusCode.BadRequest,
                    $"The cookie {TempDataCookieKey} could not be read.",
                    ex);
            }

            var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if ((cookie == null) || String.IsNullOrEmpty(cookie.Value))
            {
                return dictionary;
            }

            try
            {
                // Unprotect (decrypt/verify) the cookie value
                byte[] unprotectedBytes = MachineKey.Unprotect(
                    Convert.FromBase64String(cookie.Value), TempDataCookieKey);
                string json = Encoding.UTF8.GetString(unprotectedBytes);

                // Deserialize back to dictionary
                var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (deserialized != null)
                {
                    foreach (var kvp in deserialized)
                        dictionary[kvp.Key] = kvp.Value;
                }
            }
            catch
            {
                //silently ignore incorrect cookie values
            }

            // Only clear the cookie if it was present in the request
            if (_httpContext.Response != null && _httpContext.Response.Cookies != null)
            {
                var responseCookie = _httpContext.Response.Cookies.Get(TempDataCookieKey);
                if (responseCookie != null)
                {
                    responseCookie.Expires = DateTime.MinValue;
                    responseCookie.Value = String.Empty;
                }
            }

            return dictionary;
        }

        protected virtual void SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            // Only save TempData as a cookie if the user has consented or if there was already a TempData cookie
            // This prevents setting non-essential cookies without user consent
            var hasExistingCookie = _httpContext.Request?.Cookies[TempDataCookieKey] != null;
            var canWriteCookies = CanWriteNonEssentialCookies();

            if (values.Count > 0 && (hasExistingCookie || canWriteCookies))
            {
                // Serialize dictionary to JSON
                string json = JsonConvert.SerializeObject(values);

                // Protect (encrypt/sign) the JSON
                string protectedJson = Convert.ToBase64String(
                MachineKey.Protect(Encoding.UTF8.GetBytes(json), TempDataCookieKey));

                var cookie = new HttpCookie(TempDataCookieKey, protectedJson)
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                };
                _httpContext.Response.Cookies.Add(cookie);
            }
        }

        private bool CanWriteNonEssentialCookies()
        {
            // Check if cookie consent has been granted
            // This uses the same mechanism as the CookieComplianceHttpModule
            if (_httpContext?.Items?[ServicesConstants.CookieComplianceCanWriteAnalyticsCookies] is bool canWrite)
            {
                return canWrite;
            }

            // If consent information is not available, default to false for safety
            return false;
        }
    }
}
