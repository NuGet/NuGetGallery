// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using Xunit;

namespace NuGetGallery.FunctionalTests.ErrorHandling
{
    /// <summary>
    /// A collection of higher-order functions for validating a <see cref="TestResponse"/>. Note that some of these
    /// implementations assume that the <see cref="HttpClient"/> that produced the response had redirects turned off.
    /// For example, the <see cref="Redirect(string)"/> function asserts that the response is a 302 Found. This is not
    /// exactly what the user experiences because the web browser follows redirects.
    /// </summary>
    public static class Validator
    {
        private const string GenericExceptionReasonPhrase =
            "Processing of the HTTP request resulted in an exception. Please see the HTTP response returned by " +
            "the 'Response' property of this exception for details.";

        /// <summary>
        /// Asserts the response has no content or content type.
        /// </summary>
        public static Action<TestResponse> Empty(HttpStatusCode statusCode, SimulatedErrorType simulatedErrorType)
        {
            return response =>
            {
                Assert.Equal(statusCode, response.StatusCode);
                Assert.Equal(GetSimulatedErrorReasonPhrase(simulatedErrorType), response.ReasonPhrase);
                Assert.Null(response.ContentTypeHeader);
                Assert.Null(response.LocationHeader);
                Assert.Equal(0, response.ContentLength);
                Assert.Empty(response.Content);
            };
        }

        /// <summary>
        /// Asserts the response is a redirect to the specified error page.
        /// </summary>
        public static Action<TestResponse> Redirect(string errorPageName)
        {
            return response =>
            {
                Assert.Equal(HttpStatusCode.Found, response.StatusCode);
                Assert.Equal(GetDefaultReasonPhrase(HttpStatusCode.Found), response.ReasonPhrase);
                AssertContentTypeIsHtml(response);
                Assert.NotNull(response.LocationHeader);

                const string errorsPrefix = "/Errors/";
                Assert.StartsWith(errorsPrefix, response.LocationHeader);

                var errorNumberAndQueryString = response.LocationHeader.Substring(errorsPrefix.Length);
                var pieces = errorNumberAndQueryString.Split(new[] { '?' }, 2);
                Assert.Equal(2, pieces.Length);
                var actualErrorPageName = pieces[0];
                var queryString = pieces[1];

                var questionMarkIndex = response.RelativePath.IndexOf('?');
                var expectedRelativePath = response.RelativePath;
                if (questionMarkIndex >= 0)
                {
                    expectedRelativePath = expectedRelativePath.Substring(0, questionMarkIndex);
                }

                Assert.Equal($"aspxerrorpath={expectedRelativePath}", queryString);
                Assert.Contains(errorPageName, actualErrorPageName);

                AssertContentIsNotPrettyHtml(response);
            };
        }

        /// <summary>
        /// Asserts the response is XML.
        /// </summary>
        public static Action<TestResponse> Xml()
        {
            return response =>
            {
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                Assert.Equal(GetDefaultReasonPhrase(HttpStatusCode.InternalServerError), response.ReasonPhrase);
                AssertContentTypeIsXml(response);
                Assert.Null(response.LocationHeader);
                Assert.NotEqual(0, response.ContentLength);
                Assert.True(response.IsErrorXml, "The content should contain the generic XML error message.");
            };
        }

        /// <summary>
        /// Asserts the response is a simple HTML page with a generic exception message in the reason phrase.
        /// </summary>
        public static Action<TestResponse> SimpleHtmlWithExceptionReasonPhrase()
        {
            return SimpleHtml(HttpStatusCode.InternalServerError, GenericExceptionReasonPhrase);
        }

        /// <summary>
        /// Asserts the response is a simple HTML page with the default reason phrase for the specific status code.
        /// </summary>
        public static Action<TestResponse> SimpleHtml(HttpStatusCode statusCode)
        {
            return SimpleHtml(statusCode, GetDefaultReasonPhrase(statusCode));
        }

        /// <summary>
        /// Asserts the response is a simple HTML page with a reason phrase containing a simulated error type. This
        /// essentially is the behavior that NuGet client depends on to get error messages from HTTP responses. Error
        /// messages come back in the HTTP response reason phrase.
        /// </summary>
        public static Action<TestResponse> SimpleHtml(HttpStatusCode statusCode, SimulatedErrorType simulatedErrorType)
        {
            return SimpleHtml(statusCode, GetSimulatedErrorReasonPhrase(simulatedErrorType));
        }

        /// <summary>
        /// Asserts the response is the pretty HTML error page.
        /// </summary>
        public static Action<TestResponse> PrettyHtml(HttpStatusCode statusCode)
        {
            return response =>
            {
                Assert.Equal(statusCode, response.StatusCode);
                Assert.Equal(GetDefaultReasonPhrase(statusCode), response.ReasonPhrase);
                AssertContentTypeIsHtml(response);
                Assert.Null(response.LocationHeader);
                Assert.NotEqual(0, response.ContentLength);
                Assert.NotEmpty(response.Content);
                AssertContentIsPrettyHtml(response);
            };
        }

        /// <summary>
        /// Asserts the response is the pretty HTML error page for HTTP 500 Internal Server Error.
        /// </summary>
        public static Action<TestResponse> PrettyInternalServerError()
        {
            return PrettyHtml(HttpStatusCode.InternalServerError);
        }

        private static Action<TestResponse> SimpleHtml(HttpStatusCode statusCode, string reasonPhrase)
        {
            return response =>
            {
                Assert.Equal(statusCode, response.StatusCode);
                Assert.Equal(reasonPhrase, response.ReasonPhrase);
                AssertContentTypeIsHtml(response);
                Assert.Null(response.LocationHeader);
                Assert.NotEqual(0, response.ContentLength);
                Assert.NotEmpty(response.Content);
                AssertContentIsNotPrettyHtml(response);
            };
        }

        private static void AssertContentIsNotPrettyHtml(TestResponse response)
        {
            Assert.False(response.IsPrettyHtml, "The content should not be the pretty HTML error page.");
        }

        private static void AssertContentIsPrettyHtml(TestResponse response)
        {
            Assert.True(response.IsPrettyHtml, "The content should be the pretty HTML error page.");
        }

        private static void AssertContentTypeIsXml(TestResponse response)
        {
            Assert.StartsWith("application/xml", response.ContentTypeHeader);
        }

        private static void AssertContentTypeIsHtml(TestResponse response)
        {
            Assert.StartsWith("text/html", response.ContentTypeHeader);
        }

        private static string GetDefaultReasonPhrase(HttpStatusCode statusCode)
        {
            using (var response = new HttpResponseMessage(statusCode))
            {
                return response.ReasonPhrase;
            }
        }

        private static string GetSimulatedErrorReasonPhrase(SimulatedErrorType simulatedErrorType)
        {
            return $"SimulatedErrorType {simulatedErrorType}";
        }
    }
}