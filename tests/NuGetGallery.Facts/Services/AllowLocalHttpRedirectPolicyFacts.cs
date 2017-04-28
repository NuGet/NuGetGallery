// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.Services
{
    public class AllowLocalHttpRedirectPolicyFacts
    {
        private const string HttpSourceUrl = "http://sourceUrl";
        private const string HttpsSourceUrl = "https://sourceUrl";
        private const string HttpDestinationUrl = "http://destinationUrl";
        private const string HttpsDestinationUrl = "https://destinationUrl";
        private const string HttpNumericLocalhostDestinationUrl = "http://127.0.0.1";
        private const string HttpLocalhostDestinationUrl = "http://localhost";
        // RFC2732 style numeric IPv6 URL
        private const string HttpIpv6LocalhostDestinationUrl = "http://[::1]";

        [Theory]
        [InlineData(HttpSourceUrl, HttpDestinationUrl, true)]
        [InlineData(HttpSourceUrl, HttpsDestinationUrl, true)]
        [InlineData(HttpsSourceUrl, HttpDestinationUrl, false)]
        [InlineData(HttpsSourceUrl, HttpsDestinationUrl, true)]
        public void WillOnlyAllowSafeRedirects(string sourceUrl, string destinationUrl, bool expectedResult)
        {
            var redirectPolicy = new AllowLocalHttpRedirectPolicy();
            Assert.Equal(expectedResult, redirectPolicy.IsAllowed(new Uri(sourceUrl), new Uri(destinationUrl)));
        }

        [Theory]
        [InlineData(HttpsSourceUrl, HttpNumericLocalhostDestinationUrl)]
        [InlineData(HttpsSourceUrl, HttpLocalhostDestinationUrl)]
        [InlineData(HttpsSourceUrl, HttpIpv6LocalhostDestinationUrl)]
        public void WillAllowLocalhostRedirects(string sourceUrl, string destinationUrl)
        {
            var redirectPolicy = new AllowLocalHttpRedirectPolicy();
            Assert.True(redirectPolicy.IsAllowed(new Uri(sourceUrl), new Uri(destinationUrl)));
        }
    }
}
