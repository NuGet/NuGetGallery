// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Search;
using Xunit;

namespace NuGetGallery
{
    public class AutocompleteServiceQueryFacts
    {
        private readonly static Uri _baseAddress = new Uri("https://api.nuget.org");

        [Theory]
        [InlineData("someQueryString", "{\r\n  \"queryString\": \"?someQueryString\",\r\n  \"path\": \"/autocomplete\"\r\n}")]
        [InlineData("?someQueryString", "{\r\n  \"queryString\": \"?someQueryString\",\r\n  \"path\": \"/autocomplete\"\r\n}")]

        public async Task ExecuteQueryReturnsTheExpectedString(string queryString, string expectedResult)
        {
            // Arrange
            var autocompleteServiceQuery = TestAutocompleteServiceQuery.Instance(_baseAddress, queryString);

            // Act
            var result = await autocompleteServiceQuery.ExecuteQuery(queryString);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("someQueryString", true, null, "?someQueryString&prerelease=True")]
        [InlineData("someQueryString", true, "1.0.0-beta.20.5", "?someQueryString&prerelease=True&semVerLevel=1.0.0-beta.20.5")]
        [InlineData("someQueryString", true, "1.0.1+security.patch.2349", "?someQueryString&prerelease=True&semVerLevel=1.0.1+security.patch.2349")]
        [InlineData("&someQueryString", false, "1.0.0-beta.20.5", "?someQueryString&prerelease=False&semVerLevel=1.0.0-beta.20.5")]
        [InlineData("someQueryString", null, "1.0.0-beta.20.5", "?someQueryString&prerelease=False&semVerLevel=1.0.0-beta.20.5")]
        [InlineData("", null, "1.0.0-beta.20.5", "?prerelease=False&semVerLevel=1.0.0-beta.20.5")]
        [InlineData(null, null, "1.0.0-beta.20.5", "?prerelease=False&semVerLevel=1.0.0-beta.20.5")]
        [InlineData("someQueryString", true, "", "?someQueryString&prerelease=True")]
        public void BuildQueryStringReturnsTheExpectedString(string queryString, bool? includePrerelease, string semVerLevel, string expectedResult)
        {
            // Arrange
            var autocompleteServiceQuery = TestAutocompleteServiceQuery.Instance(_baseAddress, queryString);

            // Act
            var result = autocompleteServiceQuery.BuildQueryString(queryString, includePrerelease, semVerLevel);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        private class TestAutocompleteServiceQuery
        {
            AutocompleteServiceQuery _instance;
            HttpResponseMessage _responseMessage;
            private readonly string _autocompletePath = "autocomplete";

            private TestAutocompleteServiceQuery(Uri baseAddress, string queryString)
            {
                var mockConfiguration = new Mock<IAppConfiguration>();

                _responseMessage = GetResponseMessage(new Uri(baseAddress, $"{_autocompletePath}?{queryString?.TrimStart('?')??string.Empty}"), HttpStatusCode.OK);
                var mockIResilientSearchClient = new Mock<IResilientSearchClient>();
                mockIResilientSearchClient.Setup(s => s.GetAsync(_autocompletePath, It.IsAny<string>())).ReturnsAsync(_responseMessage);

                _instance = new AutocompleteServiceQuery(mockConfiguration.Object, mockIResilientSearchClient.Object);
            }

            private static HttpResponseMessage GetResponseMessage(Uri uri, HttpStatusCode statusCode)
            {
                string path = uri.AbsolutePath;
                string queryString = uri.Query;

                var content = new JObject(
                               new JProperty("queryString", queryString),
                               new JProperty("path", path));

                return new HttpResponseMessage()
                {
                    Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.JsonContentType),
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                    StatusCode = statusCode
                };

            }

            public static AutocompleteServiceQuery Instance(Uri baseAddress, string queryString)
            {
                var test =  new TestAutocompleteServiceQuery(baseAddress, queryString);
                return test._instance;
            }
        }
    }
}
