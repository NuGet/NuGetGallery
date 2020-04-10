// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.ErrorHandling
{
    public class ErrorHandlingTests : GalleryTestBase, IDisposable
    {
        private readonly CookieContainer _cookieContainer;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _httpClient;

        public ErrorHandlingTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            _cookieContainer = new CookieContainer();
            _httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = true,
                CookieContainer = _cookieContainer,
            };
            _httpClient = new HttpClient(_httpClientHandler);
        }

        /// <summary>
        /// Verify the behavior when a corrupted cookie is sent back to the server.
        /// </summary>
        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [MemberData(nameof(RejectedCookies))]
        public async Task RejectedCookie(string relativePath, string name, string value, Action<TestResponse> validate)
        {
            // Arrange
            _httpClientHandler.UseCookies = false;
            var requestUri = GetRequestUri(relativePath);
            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                request.Headers.TryAddWithoutValidation("Cookie", $"{name}={value}");

                // Act
                var response = await GetTestResponseAsync(relativePath, request);

                // Assert
                validate(response);
            }
        }

        public static IEnumerable<object[]> RejectedCookies => new[]
        {
            new object[] { $"/packages/{Constants.TestPackageId}", "__Controller::TempData", "Message=You successfully uploaded z̡̜͍̈̍̐̃̊͋́a̜̣͍̬̞̝͉̽ͧ͗l̸̖͕̤̠̹̘͖̃̌ͤg͓̝͓̰̀ͪo͈͌ 1.0.0.", Validator.SimpleHtml(HttpStatusCode.BadRequest) },

            new object[] { $"/packages/{Constants.TestPackageId}", "__Controller::TempData", "Message=<script>alert(1)</script>", Validator.Redirect("500") },

            new object[] { $"/packages/{Constants.TestPackageId}", "__Controller::TempData", "<script>alert(1)</script>", Validator.Redirect("500") },

            new object[] { "/packages", "nugetab", "<script>alert(1)</script>", Validator.PrettyInternalServerError() },

            new object[] { "/packages", "nugetab", "z̡̜͍̈̍̐̃̊͋́a̜̣͍̬̞̝͉̽ͧ͗l̸̖͕̤̠̹̘͖̃̌ͤg͓̝͓̰̀ͪo͈͌", Validator.SimpleHtml(HttpStatusCode.BadRequest) },
        };

        /// <summary>
        /// Verify the behavior when a URL with restricted characters is used.
        /// </summary>
        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [InlineData("/api/v2/lazy/%3E=1.0.5%20%3C1.1")]
        [InlineData("/api/v2/mysql/*")]
        [InlineData("/api/v2/comb/%3E=0.0.2")]
        [InlineData("/.nuGetV3/feed.json:properties")]
        [InlineData("/.nuGetV3/registration-semver2/serilog/page/0.1.6/1.2.47.json:properties")]
        public async Task RejectedUrl(string relativePath)
        {
            // Arrange & Act
            var response = await GetTestResponseAsync(relativePath);

            // Assert
            // Since the HTTP client is configured to not follow redirects, the response we get back is not the
            // error page itself but instead a redirect to an error page.
            Validator.Redirect("400")(response);
        }

        /// <summary>
        /// Verify simple 404 behavior.
        /// </summary>
        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [InlineData("/api/does-not-exist")]
        [InlineData("/pages/does-not-exist")]
        [InlineData("/api/v2/curated-feed/microsoftdotnet/DoesNotExist()")]
        [InlineData("/does-not-exist")]
        public async Task PageThatDoesNotExist(string relativePath)
        {
            // Arrange & Act
            var response = await GetTestResponseAsync(relativePath);

            // Assert
            Validator.PrettyHtml(HttpStatusCode.NotFound)(response);
        }

        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task DefaultErrorPageBehavior()
        {
            // Arrange & Act
            var response = await GetTestResponseAsync("/Errors/500");

            // Assert
            Validator.PrettyInternalServerError()(response);
        }

        /// <summary>
        /// Verify behavior when the pretty HTTP 500 page fails itself.
        /// </summary>
        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task ErrorInErrorPageWithoutPath()
        {
            // Arrange
            var cookies = new SimulatedErrorRequest(EndpointType.Pages, SimulatedErrorType.ExceptionInDedicatedErrorPage).GetCookies();

            // Act
            var response = await GetTestResponseAsync("/Errors/500", cookies);

            // Assert
            Validator.Redirect("500")(response);
            Assert.Equal("/Errors/500?aspxerrorpath=/Errors/500", response.LocationHeader);
        }

        /// <summary>
        /// Verify behavior when the pretty HTTP 500 page fails itself.
        /// </summary>
        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task ErrorInErrorPageWithPathToSelf()
        {
            // Arrange
            var cookies = new SimulatedErrorRequest(EndpointType.Pages, SimulatedErrorType.ExceptionInDedicatedErrorPage).GetCookies();

            // Act
            var response = await GetTestResponseAsync("/Errors/500?aspxerrorpath=/Errors/500", cookies);

            // Assert
            Validator.SimpleHtml(HttpStatusCode.InternalServerError)(response);
            Assert.Contains(
                "An exception occurred while processing your request. " +
                "Additionally, another exception occurred while executing the custom error page for the first exception. " +
                "The request has been terminated.",
                response.Content);
        }

        /// <summary>
        /// Simulate cases where application code throws different sorts of exceptions.
        /// </summary>
        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [MemberData(nameof(AllTestData))]
        public async Task SimulateError(EndpointType endpointType, SimulatedErrorType simulatedErrorType)
        {
            // Arrange
            var request = new SimulatedErrorRequest(endpointType, simulatedErrorType);

            // Act
            var response = await GetTestResponseAsync(request);

            // Assert
            if (!ExpectedSimulatedErrorResponses.TryGetValue(request, out var validator))
            {
                validator = Validator.PrettyInternalServerError();
            }

            validator(response);
        }

        /// <summary>
        /// This enumerates all combinations of <see cref="EndpointType"/> and <see cref="SimulatedErrorRequest"/> for
        /// testing purposes.
        /// </summary>
        public static IEnumerable<object[]> AllTestData
        {
            get
            {
                foreach (var endpoint in Enum.GetValues(typeof(EndpointType)).Cast<EndpointType>())
                {
                    foreach (var type in Enum.GetValues(typeof(SimulatedErrorType)).Cast<SimulatedErrorType>())
                    {
                        yield return new object[] { endpoint, type };
                    }
                }
            }
        }

        /// <summary>
        /// The expected responses for a <see cref="SimulatedErrorRequest"/>. If a key is not present, the
        /// <see cref="SimulateError(EndpointType, SimulatedErrorType)"/> test will use the
        /// <see cref="Validator.PrettyInternalServerError"/> validator.
        /// </summary>
        public static IReadOnlyDictionary<SimulatedErrorRequest, Action<TestResponse>> ExpectedSimulatedErrorResponses = new Dictionary<SimulatedErrorRequest, Action<TestResponse>>
        {
            { SER(EndpointType.Api, SimulatedErrorType.Exception), Validator.SimpleHtml(HttpStatusCode.InternalServerError, SimulatedErrorType.Exception) },
            { SER(EndpointType.Api, SimulatedErrorType.HttpException500), Validator.SimpleHtml(HttpStatusCode.InternalServerError, SimulatedErrorType.HttpException500) },
            { SER(EndpointType.Api, SimulatedErrorType.HttpResponseException400), Validator.SimpleHtmlWithExceptionReasonPhrase() },
            { SER(EndpointType.Api, SimulatedErrorType.HttpResponseException404), Validator.SimpleHtmlWithExceptionReasonPhrase() },
            { SER(EndpointType.Api, SimulatedErrorType.HttpResponseException500), Validator.SimpleHtmlWithExceptionReasonPhrase() },
            { SER(EndpointType.Api, SimulatedErrorType.HttpResponseException503), Validator.SimpleHtmlWithExceptionReasonPhrase() },
            { SER(EndpointType.Api, SimulatedErrorType.ReadOnlyMode), Validator.SimpleHtml(HttpStatusCode.ServiceUnavailable, SimulatedErrorType.ReadOnlyMode) },
            { SER(EndpointType.Api, SimulatedErrorType.Result400), Validator.SimpleHtml(HttpStatusCode.BadRequest, SimulatedErrorType.Result400) },
            { SER(EndpointType.Api, SimulatedErrorType.Result404), Validator.PrettyHtml(HttpStatusCode.NotFound) },
            { SER(EndpointType.Api, SimulatedErrorType.Result503), Validator.SimpleHtml(HttpStatusCode.ServiceUnavailable, SimulatedErrorType.Result503) },
            { SER(EndpointType.Api, SimulatedErrorType.UserSafeException), Validator.SimpleHtml(HttpStatusCode.InternalServerError, SimulatedErrorType.UserSafeException) },
            { SER(EndpointType.Api, SimulatedErrorType.ExceptionInView), Validator.SimpleHtml(HttpStatusCode.InternalServerError, SimulatedErrorType.ExceptionInView) },
            { SER(EndpointType.Api, SimulatedErrorType.ExceptionInInlineErrorPage), Validator.SimpleHtml(HttpStatusCode.InternalServerError, SimulatedErrorType.ExceptionInInlineErrorPage) },
            { SER(EndpointType.Api, SimulatedErrorType.ExceptionInDedicatedErrorPage), Validator.SimpleHtml(HttpStatusCode.InternalServerError, SimulatedErrorType.ExceptionInDedicatedErrorPage) },
            { SER(EndpointType.OData, SimulatedErrorType.Exception), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.ExceptionInView), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.ExceptionInInlineErrorPage), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.ExceptionInDedicatedErrorPage), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.HttpException400), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.HttpException404), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.HttpException500), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.HttpException503), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.HttpResponseException400), Validator.Empty(HttpStatusCode.BadRequest, SimulatedErrorType.HttpResponseException400) },
            { SER(EndpointType.OData, SimulatedErrorType.HttpResponseException404), Validator.Empty(HttpStatusCode.NotFound, SimulatedErrorType.HttpResponseException404) },
            { SER(EndpointType.OData, SimulatedErrorType.HttpResponseException500), Validator.Empty(HttpStatusCode.InternalServerError, SimulatedErrorType.HttpResponseException500) },
            { SER(EndpointType.OData, SimulatedErrorType.HttpResponseException503), Validator.Empty(HttpStatusCode.ServiceUnavailable, SimulatedErrorType.HttpResponseException503) },
            { SER(EndpointType.OData, SimulatedErrorType.ReadOnlyMode), Validator.Xml() },
            { SER(EndpointType.OData, SimulatedErrorType.Result400), Validator.Empty(HttpStatusCode.BadRequest, SimulatedErrorType.Result400) },
            { SER(EndpointType.OData, SimulatedErrorType.Result404), Validator.Empty(HttpStatusCode.NotFound, SimulatedErrorType.Result404) },
            { SER(EndpointType.OData, SimulatedErrorType.Result500), Validator.Empty(HttpStatusCode.InternalServerError, SimulatedErrorType.Result500) },
            { SER(EndpointType.OData, SimulatedErrorType.Result503), Validator.Empty(HttpStatusCode.ServiceUnavailable, SimulatedErrorType.Result503) },
            { SER(EndpointType.OData, SimulatedErrorType.UserSafeException), Validator.Xml() },
            { SER(EndpointType.Pages, SimulatedErrorType.HttpException400), Validator.Redirect("400") },
            { SER(EndpointType.Pages, SimulatedErrorType.HttpException404), Validator.Redirect("404") },
            { SER(EndpointType.Pages, SimulatedErrorType.HttpException503), Validator.Redirect("500") },
            { SER(EndpointType.Pages, SimulatedErrorType.ReadOnlyMode), Validator.PrettyHtml(HttpStatusCode.ServiceUnavailable) },
            { SER(EndpointType.Pages, SimulatedErrorType.Result400), Validator.SimpleHtml(HttpStatusCode.BadRequest, SimulatedErrorType.Result400) },
            { SER(EndpointType.Pages, SimulatedErrorType.Result404), Validator.PrettyHtml(HttpStatusCode.NotFound) },
            { SER(EndpointType.Pages, SimulatedErrorType.Result503), Validator.SimpleHtml(HttpStatusCode.ServiceUnavailable, SimulatedErrorType.Result503) },
            { SER(EndpointType.Pages, SimulatedErrorType.ExceptionInInlineErrorPage), Validator.Redirect("500") },
        };

        /// <summary>
        /// This is a short-hand for instantiating a <see cref="SimulatedErrorRequest"/>.
        /// </summary>
        private static SimulatedErrorRequest SER(EndpointType endpointType, SimulatedErrorType simulatedErrorType)
        {
            return new SimulatedErrorRequest(endpointType, simulatedErrorType);
        }

        private async Task<TestResponse> GetTestResponseAsync(string relativePath, HttpRequestMessage request)
        {
            TestOutputHelper.WriteLine($"Request:  {request.Method} {request.RequestUri.AbsoluteUri}");
            var stopwatch = Stopwatch.StartNew();
            using (var httpResponseMessage = await _httpClient.SendAsync(request))
            {
                var response = await TestResponse.FromHttpResponseMessageAsync(relativePath, httpResponseMessage);
                TestOutputHelper.WriteLine($"Duration: {stopwatch.ElapsedMilliseconds}ms");
                TestOutputHelper.WriteLine(response.ToString());
                return response;
            }
        }

        private async Task<TestResponse> GetTestResponseAsync(SimulatedErrorRequest errorRequest)
        {
            return await GetTestResponseAsync(
                errorRequest.GetRelativePath(),
                errorRequest.GetCookies());
        }

        private async Task<TestResponse> GetTestResponseAsync(string relativePath, IReadOnlyDictionary<string, string> cookies)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, GetRequestUri(relativePath)))
            {
                foreach (var cookie in cookies)
                {
                    _cookieContainer.Add(new Uri(UrlHelper.BaseUrl), new Cookie { Name = cookie.Key, Value = cookie.Value });
                }

                return await GetTestResponseAsync(relativePath, request);
            }
        }

        private async Task<TestResponse> GetTestResponseAsync(string relativePath)
        {
            return await GetTestResponseAsync(relativePath, new Dictionary<string, string>());
        }

        private Uri GetRequestUri(string relativePath)
        {
            return new Uri(new Uri(UrlHelper.BaseUrl), relativePath);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
        }
    }
}