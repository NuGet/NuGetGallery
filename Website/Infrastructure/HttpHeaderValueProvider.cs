using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpHeaderValueProvider : IValueProvider
    {
        private readonly HashSet<string> _expectedHeaders;
        private readonly HttpRequestBase _request;

        public HttpHeaderValueProvider(HttpRequestBase request, params string[] expectedHeaders)
        {
            _request = request;
            _expectedHeaders = new HashSet<string>(expectedHeaders, StringComparer.OrdinalIgnoreCase);
        }

        public bool ContainsPrefix(string prefix)
        {
            // Only serve X- headers
            return _expectedHeaders.Contains(prefix);
        }

        public ValueProviderResult GetValue(string key)
        {
            key = "X-NuGet-" + key;
            return new ValueProviderResult(_request.Headers[key], _request.Headers[key], CultureInfo.InvariantCulture);
        }
    }
}