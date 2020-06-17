// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.TransientFaultHandling;
using Moq;
using Moq.Language.Flow;
using NuGet.Services.AzureSearch.Wrappers;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class DependencyInjectionExtensionsFacts
    {
        public class TheGetRetryPolicyMethod
        {
            public TheGetRetryPolicyMethod(ITestOutputHelper output)
            {
                LoggerFactory = new LoggerFactory().AddXunit(output);
                HttpClientHandler = new Mock<TestHttpClientHandler> { CallBase = true };

                SearchServiceClient = new SearchServiceClient(
                    "test-search-service",
                    new SearchCredentials("api-key"),
                    HttpClientHandler.Object,
                    DependencyInjectionExtensions.GetSearchDelegatingHandlers(LoggerFactory));
                SearchServiceClient.SetRetryPolicy(SingleRetry);

                IndexesOperationsWrapper = new IndexesOperationsWrapper(
                    SearchServiceClient.Indexes,
                    DependencyInjectionExtensions.GetSearchDelegatingHandlers(LoggerFactory),
                    SingleRetry,
                    LoggerFactory.CreateLogger<DocumentsOperationsWrapper>());
            }

            [Theory]
            [MemberData(nameof(NonTransientTestData))]
            public async Task DoesNotRetryNonTransientErrorsForIndexOperations(Action<ISetup<TestHttpClientHandler, Task<HttpResponseMessage>>> setup)
            {
                setup(HttpClientHandler.Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())));

                await Assert.ThrowsAnyAsync<Exception>(() => SearchServiceClient.Indexes.ListAsync());

                VerifyAttemptCount(1);
            }

            [Theory]
            [MemberData(nameof(NonTransientTestData))]
            public async Task DoesNotRetryNonTransientErrorsForDocumentOperations(Action<ISetup<TestHttpClientHandler, Task<HttpResponseMessage>>> setup)
            {
                setup(HttpClientHandler.Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())));

                await Assert.ThrowsAnyAsync<Exception>(() => IndexesOperationsWrapper.GetClient("test-index").Documents.CountAsync());

                VerifyAttemptCount(1);
            }

            [Theory]
            [MemberData(nameof(TransientTestData))]
            public async Task RetriesTransientErrorsForIndexOperations(Action<ISetup<TestHttpClientHandler, Task<HttpResponseMessage>>> setup)
            {
                setup(HttpClientHandler.Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())));

                await Assert.ThrowsAnyAsync<Exception>(() => SearchServiceClient.Indexes.ListAsync());

                VerifyAttemptCount(2);
            }

            [Theory]
            [MemberData(nameof(TransientTestData))]
            public async Task RetriesTransientErrorsForDocumentOperations(Action<ISetup<TestHttpClientHandler, Task<HttpResponseMessage>>> setup)
            {
                setup(HttpClientHandler.Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())));

                await Assert.ThrowsAnyAsync<Exception>(() => IndexesOperationsWrapper.GetClient("test-index").Documents.CountAsync());

                VerifyAttemptCount(2);
            }

            private void VerifyAttemptCount(int count)
            {
                HttpClientHandler.Verify(
                    x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(count));
            }

            public static IEnumerable<HttpStatusCode> TransientHttpStatusCodes => new[]
            {
                HttpStatusCode.RequestTimeout,
                (HttpStatusCode)429,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.BadGateway,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.GatewayTimeout,
            };

            public static IEnumerable<WebExceptionStatus> TransientWebExceptionStatuses => new[]
            {
                WebExceptionStatus.ConnectFailure,
                WebExceptionStatus.ConnectionClosed,
                WebExceptionStatus.KeepAliveFailure,
                WebExceptionStatus.ReceiveFailure,
            };

            public static IEnumerable<object[]> TransientTestData => GetTestData(TransientHttpStatusCodes, TransientWebExceptionStatuses);

            public static IEnumerable<HttpStatusCode> NonTransientHttpStatusCodes => new[]
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.NotFound,
                HttpStatusCode.Conflict,
                HttpStatusCode.NotImplemented,
            };

            public static IEnumerable<WebExceptionStatus> NonTransientWebExceptionStatuses => new[]
            {
                WebExceptionStatus.TrustFailure,
                WebExceptionStatus.NameResolutionFailure,
            };

            public static IEnumerable<object[]> NonTransientTestData => GetTestData(NonTransientHttpStatusCodes, NonTransientWebExceptionStatuses);

            private static IEnumerable<object[]> GetTestData(IEnumerable<HttpStatusCode> statusCodes, IEnumerable<WebExceptionStatus> webExceptionStatuses)
            {
                var setups = new List<Action<ISetup<TestHttpClientHandler, Task<HttpResponseMessage>>>>();

                foreach (var statusCode in statusCodes)
                {
                    setups.Add(s => s.ReturnsAsync(() => new HttpResponseMessage(statusCode) { Content = new StringContent(string.Empty) }));
                }

                foreach (var webExceptionStatus in webExceptionStatuses)
                {
                    setups.Add(s => s.ThrowsAsync(new HttpRequestException("Fail.", new WebException("Inner fail.", webExceptionStatus))));
                }

                return setups.Select(x => new object[] { x });
            }

            public RetryPolicy SingleRetry => new RetryPolicy(
                new HttpStatusCodeErrorDetectionStrategy(),
                new FixedIntervalRetryStrategy(retryCount: 1, retryInterval: TimeSpan.Zero));

            public ILoggerFactory LoggerFactory { get; }
            public Mock<TestHttpClientHandler> HttpClientHandler { get; }
            public SearchServiceClient SearchServiceClient { get; }
            public IndexesOperationsWrapper IndexesOperationsWrapper { get; }
        }
    }
}
