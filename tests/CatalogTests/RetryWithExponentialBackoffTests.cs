// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NgTests.Infrastructure;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using Xunit;
using Xunit.Abstractions;

namespace CatalogTests
{
    public class RetryWithExponentialBackoffTests
    {
        public class TheSendAsyncMethod
        {
            [Fact]
            public async Task ForcesATimeoutAtTwiceTheHttpClientTimeout()
            {
                var testHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
                testHandler
                    .Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .Returns<HttpRequestMessage, CancellationToken>(async (r, t) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30)); // This ignores the provided token to make HttpClient's built-in timeout not work.
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    });
                var exceptions = new List<(Exception Exception, TimeSpan Elapsed)>();
                var stopwatch = Stopwatch.StartNew();
                using (var httpClient = new HttpClient(testHandler.Object))
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(100);
                    var target = new RetryWithExponentialBackoff(
                        maximumRetries: 1,
                        delay: TimeSpan.Zero,
                        maximumDelay: TimeSpan.FromSeconds(30),
                        httpCompletionOption: HttpCompletionOption.ResponseContentRead,
                        e =>
                        {
                            exceptions.Add((e, stopwatch.Elapsed));
                            stopwatch.Restart();
                        });

                    var ex = await Assert.ThrowsAsync<TimeoutException>(() => target.SendAsync(
                        httpClient,
                        new Uri("https://example/v3/index.json"),
                        CancellationToken.None));
                    Assert.Equal("The operation was forcibly canceled.", ex.Message);
                    var singleEx = Assert.Single(exceptions);
                    Assert.Same(ex, singleEx.Exception);
                    Assert.True(singleEx.Elapsed < TimeSpan.FromSeconds(30));
                    testHandler.Verify(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }

            [Fact]
            public async Task UsesHttpClientBuiltInTimeoutWhenPossible()
            {
                var testHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
                testHandler
                    .Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .Returns<HttpRequestMessage, CancellationToken>(async (r, t) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), t);
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    });
                var exceptions = new List<(Exception Exception, TimeSpan Elapsed)>();
                var stopwatch = Stopwatch.StartNew();
                using (var httpClient = new HttpClient(testHandler.Object))
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(100);
                    var target = new RetryWithExponentialBackoff(
                        maximumRetries: 1,
                        delay: TimeSpan.Zero,
                        maximumDelay: TimeSpan.FromSeconds(30),
                        httpCompletionOption: HttpCompletionOption.ResponseContentRead,
                        e =>
                        {
                            exceptions.Add((e, stopwatch.Elapsed));
                            stopwatch.Restart();
                        });

                    var ex = await Assert.ThrowsAsync<TimeoutException>(() => target.SendAsync(
                        httpClient,
                        new Uri("https://example/v3/index.json"),
                        CancellationToken.None));
                    Assert.Equal("Maximum retry attempts exhausted.", ex.Message);
                    Assert.Equal(2, exceptions.Count);
                    Assert.All(exceptions, e => Assert.IsType<TaskCanceledException>(e.Exception));
                    Assert.All(exceptions, e => Assert.Equal("A task was canceled.", e.Exception.Message));
                    Assert.All(exceptions, e => Assert.True(e.Elapsed < TimeSpan.FromSeconds(30)));
                    testHandler.Verify(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
                }
            }

            [Fact]
            public async Task AllowsCancellation()
            {
                var testHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
                testHandler
                    .Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .Returns<HttpRequestMessage, CancellationToken>(async (r, t) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), t);
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    });
                var exceptions = new List<(Exception Exception, TimeSpan Elapsed)>();
                var stopwatch = Stopwatch.StartNew();
                using (var httpClient = new HttpClient(testHandler.Object))
                using (var cts = new CancellationTokenSource())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    var target = new RetryWithExponentialBackoff(
                        maximumRetries: 1,
                        delay: TimeSpan.Zero,
                        maximumDelay: TimeSpan.FromSeconds(30),
                        httpCompletionOption: HttpCompletionOption.ResponseContentRead,
                        e =>
                        {
                            exceptions.Add((e, stopwatch.Elapsed));
                            stopwatch.Restart();
                        });
                    
                    var exTask = Assert.ThrowsAsync<TimeoutException>(() => target.SendAsync(
                        httpClient,
                        new Uri("https://example/v3/index.json"),
                        cts.Token));
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    cts.Cancel();
                    var ex = await exTask;

                    Assert.Equal("Maximum retry attempts exhausted.", ex.Message);
                    Assert.Equal(2, exceptions.Count);
                    Assert.All(exceptions, e => Assert.IsType<TaskCanceledException>(e.Exception));
                    Assert.All(exceptions, e => Assert.Equal("A task was canceled.", e.Exception.Message));
                    Assert.All(exceptions, e => Assert.True(e.Elapsed < TimeSpan.FromSeconds(30)));
                    testHandler.Verify(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
                }
            }

            [Fact]
            public async Task DoesNotTimeoutWithHungResponseBody_WithResponseHeadersRead()
            {
                var testHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
                testHandler
                    .Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StreamContent(new HungStream(
                                new MemoryStream(Encoding.UTF8.GetBytes("foo")),
                                hangTime: TimeSpan.FromSeconds(30)))
                        };
                    });
                using (var httpClient = new HttpClient(testHandler.Object))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    var target = new RetryWithExponentialBackoff(
                        maximumRetries: 1,
                        delay: TimeSpan.Zero,
                        maximumDelay: TimeSpan.FromSeconds(30),
                        httpCompletionOption: HttpCompletionOption.ResponseHeadersRead,
                        onException: null);

                    var response = await target.SendAsync(
                        httpClient,
                        new Uri("https://example/v3/index.json"),
                        CancellationToken.None);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    testHandler.Verify(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }

            [Fact]
            public async Task TimesOutWithHungResponseBody_WithResponseContentRead()
            {
                var testHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
                testHandler
                    .Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StreamContent(new HungStream(
                                new MemoryStream(Encoding.UTF8.GetBytes("foo")),
                                hangTime: TimeSpan.FromSeconds(30)))
                        };
                    });
                var exceptions = new List<(Exception Exception, TimeSpan Elapsed)>();
                var stopwatch = Stopwatch.StartNew();
                using (var httpClient = new HttpClient(testHandler.Object))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(1);
                    var target = new RetryWithExponentialBackoff(
                        maximumRetries: 1,
                        delay: TimeSpan.Zero,
                        maximumDelay: TimeSpan.FromSeconds(30),
                        httpCompletionOption: HttpCompletionOption.ResponseContentRead,
                        e =>
                        {
                            exceptions.Add((e, stopwatch.Elapsed));
                            stopwatch.Restart();
                        });

                    var ex = await Assert.ThrowsAsync<TimeoutException>(() => target.SendAsync(
                        httpClient,
                        new Uri("https://example/v3/index.json"),
                        CancellationToken.None));
                    Assert.Equal("The operation was forcibly canceled.", ex.Message);
                    var singleEx = Assert.Single(exceptions);
                    Assert.Same(ex, singleEx.Exception);
                    Assert.True(singleEx.Elapsed < TimeSpan.FromSeconds(30));
                    testHandler.Verify(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        public class TheIsTransientErrorMethod
        {
            [Fact]
            public void ReturnsFalseIfNotCorrectExceptionType()
            {
                var e = new Exception();
                var result = RetryWithExponentialBackoff.IsTransientError(e, null);
                Assert.False(result);
            }

            public static IEnumerable<Exception> TransientExceptions => new Exception[] 
            {
                new HttpRequestException(),
                new OperationCanceledException()
            };

            public static IEnumerable<object[]> ReturnsTrueIfResponseNull_Data
            {
                get
                {
                    foreach (var exception in TransientExceptions)
                    {
                        yield return new object[] { exception };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsTrueIfResponseNull_Data))]
            public void ReturnsTrueIfResponseNull(Exception e)
            {
                var result = RetryWithExponentialBackoff.IsTransientError(e, null);
                Assert.True(result);
            }

            public static IEnumerable<HttpStatusCode> NonTransientStatusCodes => new[]
            {
                HttpStatusCode.Accepted,
                HttpStatusCode.Conflict,
                HttpStatusCode.NotFound,
                HttpStatusCode.OK,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.NotImplemented,
                HttpStatusCode.HttpVersionNotSupported
            };

            public static IEnumerable<object[]> ReturnsFalseIfResponseStatusBelow500OrWhitelisted_Data
            {
                get
                {
                    foreach (var exception in TransientExceptions)
                    {
                        foreach (var status in NonTransientStatusCodes)
                        {
                            yield return new object[] { exception, status };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsFalseIfResponseStatusBelow500OrWhitelisted_Data))]
            public void ReturnsFalseIfResponseStatusBelow500OrWhitelisted(Exception e, HttpStatusCode status)
            {
                var response = new HttpResponseMessage(status);
                var result = RetryWithExponentialBackoff.IsTransientError(e, response);
                Assert.False(result);
            }

            public static IEnumerable<HttpStatusCode> TransientStatusCodes => new[]
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.BadGateway,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.GatewayTimeout
            };

            public static IEnumerable<object[]> ReturnsTrueIfResponseStatusAbove500_Data
            {
                get
                {
                    foreach (var exception in TransientExceptions)
                    {
                        foreach (var status in TransientStatusCodes)
                        {
                            yield return new object[] { exception, status };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsTrueIfResponseStatusAbove500_Data))]
            public void ReturnsTrueIfResponseStatusAbove500(Exception e, HttpStatusCode status)
            {
                var response = new HttpResponseMessage(status);
                var result = RetryWithExponentialBackoff.IsTransientError(e, response);
                Assert.True(result);
            }
        }
    }
}
