// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core.Models;
using Newtonsoft.Json;
using Xunit;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class StartupFunctionalTests : BaseFunctionalTests
    {
        private static string RegistrationBaseUrl = "RegistrationsBaseUrl";
        private const int IndexDifferenceLimitInHrs = 1;

        [Fact]
        public async Task Ready()
        {
            // Act
            var response = await Client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("READY", content);
        }

        [Fact]
        public async Task InvalidEndpoint()
        {
            // Act
            var response = await Client.GetAsync("/invalid");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("UNRECOGNIZED", content);
        }

        [Fact]
        public async Task IndexIsFresh()
        {
            var response = await Client.GetAsync("/search/diag");
            var content = await response.Content.ReadAsAsync<SearchDiagResult>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content);

            var lastRegistrationCommitTime = GetLastRegistrationCommitTime().Result;
            var diffTimes = lastRegistrationCommitTime.Subtract(content.CommitUserData.CommitTimeStamp).TotalHours;
            //Last CommitTimeStamp for search service shouldn't be far off from the last registration timestamp.
            Assert.True(diffTimes >= 0, 
                $"[{DateTime.UtcNow:O}] Search index is ahead of last registration timestamp " +
                $"(Registration: {lastRegistrationCommitTime:O}, Search: {content.CommitUserData.CommitTimeStamp:O})");
            Assert.True(diffTimes <= IndexDifferenceLimitInHrs, 
                $"[{DateTime.UtcNow:O}] Search index is too much behind the last registration timestamp " +
                $"(Registration: {lastRegistrationCommitTime:O}, Search: {content.CommitUserData.CommitTimeStamp:O})");
        }

        private static string[] PathsToTest => new[]
        {
            "/",
        };

        private static HttpMethod[] RedirectAllowedHttpMethods => new[]
        {
            HttpMethod.Get,
            HttpMethod.Head,
        };

        private static HttpMethod[] RedirectNotAllowedHttpMethods => new[]
        {
            HttpMethod.Post,
            HttpMethod.Options,
            HttpMethod.Put,
            HttpMethod.Delete,
            HttpMethod.Trace,
        };

        public static IEnumerable<object[]> ExcludedPaths => new[]
        {
            new object[] { "/search/diag" },
        };

        public static IEnumerable<object[]> AllowedRedirectsToCheck =>
            from url in PathsToTest
            from method in RedirectAllowedHttpMethods
            select new object[] { method, url };

        public static IEnumerable<object[]> NotAllowedRedirectsToCheck =>
            from url in PathsToTest
            from method in RedirectNotAllowedHttpMethods
            select new object[] { method, url };

        [Theory]
        [MemberData(nameof(AllowedRedirectsToCheck))]
        public async Task HttpToHttpsRedirectOccurs(HttpMethod method, string path)
        {
            await ValidateResponse(method, path, response =>
            {
                Assert.Equal(HttpStatusCode.Found, response.StatusCode);
                Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location.Scheme);
                Assert.Equal(path, response.Headers.Location.PathAndQuery);
            });
        }

        [Theory]
        [MemberData(nameof(NotAllowedRedirectsToCheck))]
        public async Task NonGetHeadMethodsProduceError(HttpMethod method, string path)
        {
            await ValidateResponse(method, path, response =>
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            });
        }

        [Theory]
        [MemberData(nameof(ExcludedPaths))]
        public async Task ExcludedUrlsDontRedirect(string path)
        {
            await ValidateResponse(HttpMethod.Get, path, 
                response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }

        [Fact]
        public async Task ValidateDiagnosticsMetadata()
        {
            // Act
            var response = await Client.GetAsync("/search/diag");
            var content = await response.Content.ReadAsAsync<SearchDiagResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(content);
            Assert.False(string.IsNullOrEmpty(content.MachineName), "Machine name should be specified");
            Assert.False(string.IsNullOrEmpty(content.IndexName), "IndexName should be specified");
            Assert.NotNull(content.LastReopen);
            Assert.NotNull(content.LastIndexReloadTime);
            Assert.NotNull(content.LastAuxiliaryDataLoadTime);
            Assert.True(content.NumDocs > 0, "No data loaded in search index");
            Assert.True(content.LastIndexReloadDurationInMilliseconds >= 0, "Search index was never loaded");

            var properties = typeof(AuxiliaryFilesUpdateTime).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                Assert.NotNull(property.GetValue(content.LastAuxiliaryDataUpdateTime));
            }
        }

        private Uri ForceHttp(string url)
        {
            if (url.StartsWith(Uri.UriSchemeHttps))
            {
                url = Uri.UriSchemeHttp + url.Substring(Uri.UriSchemeHttps.Length);
            }

            return new Uri(url);
        }

        private async Task ValidateResponse(HttpMethod method, string path, Action<HttpResponseMessage> validateResponse)
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };

            using (var client = new HttpClient(handler) { BaseAddress = ForceHttp(EnvironmentSettings.SearchServiceBaseUrl) })
            using (var response = await client.SendAsync(new HttpRequestMessage(method, path)))
            {
                validateResponse(response);
            }
        }

        private async Task<DateTime> GetLastRegistrationCommitTime()
        {
            var httpClient = new HttpClient(RetryHandler);
            var indexResponse = await httpClient.GetAsync(EnvironmentSettings.IndexBaseUrl);
            indexResponse.EnsureSuccessStatusCode();

            var endpointContent = await indexResponse.Content.ReadAsStringAsync();
            var endpointsList = JsonConvert.DeserializeObject<ServiceEndpointsList>(endpointContent);
            var resources = endpointsList.Resources;

            string registrationBaseUrl = null;
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i].AtType == RegistrationBaseUrl)
                {
                    registrationBaseUrl = resources[i].AtId;
                    break;
                }
            }

            Assert.False(registrationBaseUrl == null, "Failed to get registration base url. Please check that index url is correct.");

            var cursorUri = new Uri(new Uri(registrationBaseUrl), "cursor.json");
            var registrationResponse = await httpClient.GetAsync(cursorUri);
            registrationResponse.EnsureSuccessStatusCode();

            var registrationCursorContent = await registrationResponse.Content.ReadAsStringAsync();
            var lastRegistrationCommitTime = JsonConvert.DeserializeObject<RegistrationCursor>(registrationCursorContent).Value;
            return lastRegistrationCommitTime;
        }
    }
}
