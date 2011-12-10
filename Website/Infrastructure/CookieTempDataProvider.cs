using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class CookieTempDataProvider : ITempDataProvider
    {
        // Fields
        HttpContextBase httpContext;
        const string TempDataCookieKey = "__Controller::TempData";

        // Methods
        public CookieTempDataProvider(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException("httpContext");
            }
            this.httpContext = httpContext;
        }

        protected virtual IDictionary<string, object> LoadTempData(ControllerContext controllerContext)
        {
            var cookie = httpContext.Request.Cookies[TempDataCookieKey];
            var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if ((cookie == null) || String.IsNullOrEmpty(cookie.Value))
            {
                return dictionary;
            }
            foreach (var key in cookie.Values.AllKeys)
            {
                dictionary[key] = cookie[key];
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

        private bool CookieHasTempData
        {
            get
            {
                return ((httpContext.Response != null) && (httpContext.Response.Cookies != null)) && (httpContext.Response.Cookies[TempDataCookieKey] != null);
            }
        }

        protected virtual void SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            if (values.Count > 0)
            {
                var cookie = new HttpCookie(TempDataCookieKey);
                cookie.HttpOnly = true;
                foreach (var item in values)
                {
                    cookie[item.Key] = Convert.ToString(item.Value, CultureInfo.InvariantCulture);
                }
                httpContext.Response.Cookies.Add(cookie);
            }
        }

        IDictionary<string, object> ITempDataProvider.LoadTempData(ControllerContext controllerContext)
        {
            return LoadTempData(controllerContext);
        }

        void ITempDataProvider.SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            SaveTempData(controllerContext, values);
        }

        // Properties
        public HttpContextBase HttpContext
        {
            get
            {
                return httpContext;
            }
        }
    }
}