using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery {
    public class CookieTempDataProvider : ITempDataProvider {
        // Fields
        HttpContextBase httpContext;
        const string TempDataCookieKey = "__Controller::TempData";

        // Methods
        public CookieTempDataProvider(HttpContextBase httpContext) {
            if (httpContext == null) {
                throw new ArgumentNullException("httpContext");
            }
            this.httpContext = httpContext;
        }

        public static IDictionary<string, object> Base64StringToDictionary(string base64EncodedSerializedTempData) {
            using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(base64EncodedSerializedTempData))) {
                var formatter = new BinaryFormatter();
                return (formatter.Deserialize(stream, null) as IDictionary<string, object>);
            }
        }

        public static string DictionaryToBase64String(IDictionary<string, object> values) {
            using (MemoryStream stream = new MemoryStream()) {
                stream.Seek(0L, SeekOrigin.Begin);
                new BinaryFormatter().Serialize(stream, values);
                stream.Seek(0L, SeekOrigin.Begin);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        protected virtual IDictionary<string, object> LoadTempData(ControllerContext controllerContext) {
            var cookie = httpContext.Request.Cookies[TempDataCookieKey];
            if ((cookie == null) || string.IsNullOrEmpty(cookie.Value)) {
                return new Dictionary<string, object>();
            }
            var dictionary = Base64StringToDictionary(cookie.Value);
            cookie.Expires = DateTime.MinValue;
            cookie.Value = string.Empty;
            if (CookieHasTempData) {
                cookie.Expires = DateTime.MinValue;
                cookie.Value = string.Empty;
            }
            return dictionary;
        }

        private bool CookieHasTempData {
            get {
                return ((httpContext.Response != null) && (httpContext.Response.Cookies != null)) && (httpContext.Response.Cookies[TempDataCookieKey] != null);
            }
        }

        protected virtual void SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values) {
            if (values.Count > 0) {
                string str = DictionaryToBase64String(values);
                var cookie = new HttpCookie(TempDataCookieKey);
                cookie.HttpOnly = true;
                cookie.Value = str;
                httpContext.Response.Cookies.Add(cookie);
            }
        }

        IDictionary<string, object> ITempDataProvider.LoadTempData(ControllerContext controllerContext) {
            return LoadTempData(controllerContext);
        }

        void ITempDataProvider.SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values) {
            SaveTempData(controllerContext, values);
        }

        // Properties
        public HttpContextBase HttpContext {
            get {
                return httpContext;
            }
        }
    }

}