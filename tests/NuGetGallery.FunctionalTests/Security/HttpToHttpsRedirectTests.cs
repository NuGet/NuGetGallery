// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Security
{
    public class HttpToHttpsRedirectTests
    {
        public static IEnumerable<object[]> UrlsToTest => new[] {
            new object[] { UrlHelper.BaseUrl },
            new object[] { UrlHelper.LogonPageUrl },
            new object[] { UrlHelper.PackagesPageUrl },
            new object[] { UrlHelper.RegisterPageUrl },
            new object[] { UrlHelper.RegistrationPendingPageUrl },
            new object[] { UrlHelper.AggregateStatsPageUrl },
            new object[] { UrlHelper.UploadPageUrl },
            new object[] { UrlHelper.VerifyUploadPageUrl },
        };

        public static IEnumerable<object[]> UrlsExcludedFromRedirectInCloudService => new[]
        {
            new object[] { UrlHelper.ApiGalleryHealthProbeUrl },
            new object[] { UrlHelper.ApiStatusPageUrl }
        };

        public static IEnumerable<object[]> GetForAllUrls()
        {
            return HttpMethodsAndUrlsToTest(HttpMethod.Get);
        }

        public static IEnumerable<object[]> NonHeadAndGetForAllUrls()
        {
            return HttpMethodsAndUrlsToTest(HttpMethod.Options, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Trace);
        }

        [Theory]
        [MemberData(nameof(GetForAllUrls))]
        [Priority(0)]
        [Category("CloudServiceTests")]
        public async Task HttpToHttpsRedirectHappensForSupportedMethods(HttpMethod method, string url)
        {
            // Ideally, we should test both GET and HEAD methods for all the URLs, 
            // but the issue is that whenever we use [HttpGet] attribute on a controller
            // method we essentially block the HEAD requests.
            Uri uri = ForceHttp(url);
            await VerifyHttpResponseStatus(r => 
            {
                Assert.Equal(HttpStatusCode.Found, r.StatusCode);
                Assert.Equal(Uri.UriSchemeHttps, r.Headers.Location.Scheme);
            }, method, uri);
        }

        [Theory]
        [MemberData(nameof(NonHeadAndGetForAllUrls))]
        [Priority(0)]
        [Category("CloudServiceTests")]
        public async Task HttpRequestsFailureResponseForUnsupportedMethods(HttpMethod method, string url)
        {
            Uri uri = ForceHttp(url);
            await VerifyHttpResponseStatus(
                r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode),
                method, uri);
        }

        [Theory]
        [MemberData(nameof(UrlsToTest))]
        [Priority(0)]
        [Category("P0Tests")]
        public void ForceHttpMethodCorreclyRemovesHttps(string url)
        {
            Uri uri = ForceHttp(url);
            Assert.Equal(Uri.UriSchemeHttp, uri.Scheme);
        }

        [Theory]
        [MemberData(nameof(UrlsExcludedFromRedirectInCloudService))]
        [Priority(0)]
        [Category("CloudServiceTests")]
        public async Task ExcludedUrlsDontRedirect(string url)
        {
            Uri uri = ForceHttp(url);
            await VerifyHttpResponseStatus(
                r => Assert.Equal(HttpStatusCode.OK, r.StatusCode),
                HttpMethod.Get, uri);
        }

        public static IEnumerable<object[]> RemainingUrlsAndMethodsForAppService =>
                from url in UrlsToTest.Concat(UrlsExcludedFromRedirectInCloudService).SelectMany(x => x)
                from method in new[] { HttpMethod.Get, HttpMethod.Head, HttpMethod.Options, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Trace }
                select new object[] { method, url };

        /// <summary>
        /// This test is the app service counterpart of <see cref="HttpToHttpsRedirectHappensForSupportedMethods"/>:
        /// HTTP to HTTPS redirection there happens at the service level, so all HTTP requests are expected
        /// to respond with <see cref="HttpStatusCode.MovedPermanently"/>.
        /// </summary>
        [Theory]
        [MemberData(nameof(RemainingUrlsAndMethodsForAppService))]
        [Priority(0)]
        [Category("AppServiceTests")]
        public async Task AllUrlsRedirect(HttpMethod method, string url)
        {
            Uri uri = ForceHttp(url);
            await VerifyHttpResponseStatus(r =>
            {
                Assert.Equal(HttpStatusCode.MovedPermanently, r.StatusCode);
                Assert.Equal(Uri.UriSchemeHttps, r.Headers.Location.Scheme);
            }, method, uri);
        }

        private static async Task VerifyHttpResponseStatus(Action<HttpResponseMessage> verifyAction, HttpMethod method, Uri url)
        {
            using (HttpClient client = CreateNoRedirectsClient())
            using (HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(method, url)))
            {
                verifyAction(response);
            }
        }

        private static HttpClient CreateNoRedirectsClient()
        {
            var noRedirects = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            return new HttpClient(noRedirects);
        }

        private static Uri ForceHttp(string url)
        {
            if (url.StartsWith(Uri.UriSchemeHttps))
            {
                url = Uri.UriSchemeHttp + url.Substring(Uri.UriSchemeHttps.Length);
            }

            return new Uri(url);
        }

        private static IEnumerable<object[]> HttpMethodsAndUrlsToTest(params HttpMethod[] methodsToTest)
        {
            foreach (string url in UrlsToTest.SelectMany(x => x))
            {
                foreach (HttpMethod method in methodsToTest)
                {
                    yield return new object[] { method, url };
                }
            }
        }
    }
}
