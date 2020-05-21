// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Web;
using System.Web.Mvc;

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

            foreach (var key in cookie.Values.AllKeys)
            {
                // As the index setter on HttpCookie does not guard against null keys,
                // we should guard against ArgumentNullException on Dictionary.Insert
                // when key == null.
                if (key == null)
                {
                    continue;
                }

                dictionary[key] = HttpUtility.UrlDecode(cookie[key]);
            }

            cookie.Expires = DateTime.MinValue;
            cookie.Value = String.Empty;
            if (CookieHasTempData)
            {
                cookie.Expires = DateTime.MinValue;
                cookie.Value = String.Empty;
            }
            return dictionary;
        }

        protected virtual void SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            if (values.Count > 0)
            {
                var cookie = new HttpCookie(TempDataCookieKey);
                cookie.HttpOnly = true;
                cookie.Secure = true;
                foreach (var item in values)
                {
                    // As the index setter on HttpCookie does not guard against null keys,
                    // we should guard against ArgumentNullException on Dictionary.Insert
                    // when key == null.
                    if (item.Key == null)
                    {
                        continue;
                    }

                    cookie[item.Key] = HttpUtility.UrlEncode(Convert.ToString(item.Value, CultureInfo.InvariantCulture));
                }
                _httpContext.Response.Cookies.Add(cookie);
            }
        }

        // Properties
    }
}