// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Owin;
using Xunit;

namespace NuGetGallery
{
    public static class OwinAssert
    {
        private static readonly DateTimeOffset CookieEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);

        private static Regex _cookieRegex = new Regex(@"^\s*(?<name>[^=]*)=(?<val>[^;]*)(;(?<fields>.*))?\s*$");
        private static Regex _fieldRegex = new Regex(@"^\s*(?<name>[^=]*)=(?<val>.*)\s*$");
        
        public static void WillRedirect(IOwinContext context, string expectedLocation)
        {
            Assert.Equal(302, context.Response.StatusCode);
            Assert.Equal(expectedLocation, context.Response.Headers["Location"]);
        }

        public static void SetsCookie(IOwinResponse response, string name, string expectedValue)
        {
            // Get the cookie
            var cookie = GetCookie(response, name);

            // Check the value
            Assert.NotNull(cookie);
            Assert.Equal(expectedValue, cookie.Value);
        }

        public static void DeletesCookie(IOwinResponse response, string name)
        {
            // Get the cookie
            var cookie = GetCookie(response, name);

            // Check the value and expiry
            Assert.NotNull(cookie);
            Assert.True(String.IsNullOrEmpty(cookie.Value));
            Assert.True(cookie.Fields.ContainsKey("expires"));
            Assert.Equal(CookieEpoch, DateTimeOffset.Parse(cookie.Fields["expires"]));
        }

        private static Cookie GetCookie(IOwinResponse response, string name)
        {
            return GetCookies(response).FirstOrDefault(c => c.Name == name);
        }

        private static IEnumerable<Cookie> GetCookies(IOwinResponse response)
        {
            foreach (var cookieHeader in response.Headers.GetValues("Set-Cookie"))
            {
                var match = _cookieRegex.Match(cookieHeader);
                if (match.Success)
                {
                    var fieldStrings = match.Groups["fields"].Value.Split(';');
                    yield return new Cookie(
                        match.Groups["name"].Value,
                        match.Groups["val"].Value,
                        fieldStrings
                            .Select(s => _fieldRegex.Match(s))
                            .Where(m => m.Success)
                            .ToDictionary(
                                m => m.Groups["name"].Value, 
                                m => m.Groups["val"].Value));
                }
            }
        }

        private class Cookie
        {
            public string Name { get; private set; }
            public string Value { get; private set; }
            public IDictionary<string, string> Fields { get; private set; }

            public Cookie(string name, string value, IDictionary<string, string> fields)
            {
                Name = name;
                Value = value;
                Fields = fields;
            }
        }
    }
}
