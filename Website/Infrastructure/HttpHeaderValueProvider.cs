using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpHeaderValueProvider : IValueProvider
    {
        private readonly HttpRequestBase request;
        private readonly HashSet<string> expectedHeaders;

        public HttpHeaderValueProvider(HttpRequestBase request, params string[] expectedHeaders)
        {
            this.request = request;
            this.expectedHeaders = new HashSet<string>(expectedHeaders, StringComparer.OrdinalIgnoreCase);
        }

        public bool ContainsPrefix(string prefix)
        {
            // Only serve X- headers
            return expectedHeaders.Contains(prefix);
        }

        public ValueProviderResult GetValue(string key)
        {
            key = "X-NuGet-" + key;
            return new ValueProviderResult(request.Headers[key], request.Headers[key], CultureInfo.InvariantCulture);
        }
    }
}