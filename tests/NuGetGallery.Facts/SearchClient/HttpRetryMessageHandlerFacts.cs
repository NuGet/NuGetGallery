// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NuGet.Services.Search.Client;

namespace NuGetGallery.SearchClient
{
    public class HttpRetryMessageHandlerFacts
    {
        private List<HttpStatusCode> _retryingCodes = new List<HttpStatusCode>{
                HttpStatusCode.RequestTimeout, // 408
                HttpStatusCode.InternalServerError, // 500
                HttpStatusCode.BadGateway, // 502
                HttpStatusCode.ServiceUnavailable, // 503
                HttpStatusCode.GatewayTimeout // 504
            };

        [Fact]
        public void VerifyDefaults()
        {
            // Arrange + Act
            var handler = new HttpRetryMessageHandler((ex) => { });
            var codes = handler.GetRetryingCodes;

            // Assert
            Assert.Equal(_retryingCodes.Count, codes.Count);
            foreach (var c in _retryingCodes)
            {
                Assert.True(codes.Contains(c));
            }
        }

        [Fact]
        public void VerifyNewCodes()
        {
            // Arrange + Act
            var handler = new HttpRetryMessageHandler((ex) => { }, 1, new List<HttpStatusCode> { HttpStatusCode.NotFound });
            var codes = handler.GetRetryingCodes;

            // Assert
            Assert.Equal(_retryingCodes.Count + 1, codes.Count);
            Assert.True(codes.Contains(HttpStatusCode.NotFound));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task VerifyHandledStatusCodeWillForceReexecution(int retryCount)
        {
            // Arrange 
            Uri InvalidUriWith404 = new Uri("http://www.nuget.org/thisshouldreturna404page");
            var trackingHandler = new RequestInspectingHandler();
            var retryHandler = new HttpRetryMessageHandler((ex) => { }, retryCount, new List<HttpStatusCode> { HttpStatusCode.NotFound });

            retryHandler.InnerHandler = trackingHandler;
            HttpClient client = new HttpClient(retryHandler);

            // Act
            var message = await client.GetAsync(InvalidUriWith404);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, message.StatusCode);
            Assert.Equal(retryCount + 1, trackingHandler.Requests.Count);
        }

        public class TheRetryPolicyFacts
        {
            [Fact]
            public async Task ExceptionIsHandledAndRetrownOnRetryCountEnds()
            {
                // Arrange
                int exceptionHandlerInvoked = 0;
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;
                int retryCount = 3;

                // Act
                await Assert.ThrowsAsync<ArgumentException>( () => HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                        (ex) =>
                        {
                            Interlocked.Increment(ref exceptionHandlerInvoked);
                            return ex is ArgumentException;
                        },
                        result => false).
                        ExecuteWithWaitAndRetryAsync(retryCount,
                        (currentCount) =>
                        {
                            Interlocked.Increment(ref sleepTimeProviderInvoked);
                            return TimeSpan.FromMilliseconds(1);
                        },
                        () =>
                        {
                            return Task<int>.Run(() =>
                            {
                                Interlocked.Increment(ref executionMethodInvoked);
                                if ("ab".Length == 2)
                                {
                                    throw new ArgumentException("Test");
                                }
                                return 1;
                            });
                        }));

                // Assert
                Assert.Equal(retryCount+1, exceptionHandlerInvoked);
                Assert.Equal(retryCount+1 , executionMethodInvoked);
                Assert.Equal(retryCount, sleepTimeProviderInvoked);
            }

            [Fact]
            public async Task NullExceptionHnadlerDoesNotHandleAnything()
            {
                // Arrange
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;
                int retryCount = 3;

                // Act
                await Assert.ThrowsAsync<ArgumentException>(() => HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                       null,
                       result => false).
                       ExecuteWithWaitAndRetryAsync(retryCount,
                       (currentCount) =>
                       {
                           Interlocked.Increment(ref sleepTimeProviderInvoked);
                           return TimeSpan.FromMilliseconds(1);
                       },
                       () =>
                       {
                           return Task<int>.Run(() =>
                           {
                               Interlocked.Increment(ref executionMethodInvoked);
                               if ("ab".Length == 2)
                               {
                                   throw new ArgumentException("Test");
                               }
                               return 1;
                           });
                       }));

                // Assert
                Assert.Equal(1, executionMethodInvoked);
                Assert.Equal(0, sleepTimeProviderInvoked);
            }


            [Fact]
            public async Task ExceptionIsHandledAndNotRetrownIfItIsFixedBeforeRetryCountEnds()
            {
                // Arrange
                int exceptionHandlerInvoked = 0;
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;
                int retryCount = 3;
                int throwExceptionForThisNumberOfTimes = 1;

                // Act
                var opResult = await HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                       (ex) =>
                       {
                           Interlocked.Increment(ref exceptionHandlerInvoked);
                           return ex is ArgumentException;
                       },
                       result => false).
                       ExecuteWithWaitAndRetryAsync(retryCount,
                       (currentCount) =>
                       {
                           Interlocked.Increment(ref sleepTimeProviderInvoked);
                           return TimeSpan.FromMilliseconds(1);
                       },
                       () =>
                       {
                           return Task<int>.Run(() =>
                           {
                               int retCount = Interlocked.Increment(ref executionMethodInvoked);
                               if (retCount <= throwExceptionForThisNumberOfTimes)
                               {
                                   throw new ArgumentException("Test");
                               }
                               return retCount;
                           });
                       });

                // Assert
                Assert.Equal(throwExceptionForThisNumberOfTimes, exceptionHandlerInvoked);
                Assert.Equal(throwExceptionForThisNumberOfTimes + 1, executionMethodInvoked);
                Assert.Equal(throwExceptionForThisNumberOfTimes, sleepTimeProviderInvoked);
                Assert.Equal(executionMethodInvoked, opResult);
            }

            [Fact]
            public async Task ExceutionHandlesReturnCodesAndForceReexecution()
            {
                // Arrange
                int executionHandlerInvoked = 0;
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;
                int retryCount = 3;

                // Act
                var opResult = await HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                       null,
                       (result) =>
                       {
                           Interlocked.Increment(ref executionHandlerInvoked);
                           // force re-execution twice
                           return result == 1 || result == 2; 
                       }).
                       ExecuteWithWaitAndRetryAsync(retryCount,
                       (currentCount) =>
                       {
                           Interlocked.Increment(ref sleepTimeProviderInvoked);
                           return TimeSpan.FromMilliseconds(1);
                       },
                       () =>
                       {
                           return Task<int>.Run(() =>
                           {
                               return Interlocked.Increment(ref executionMethodInvoked);
                           });
                       });

                // Assert
                Assert.Equal(3, executionHandlerInvoked);
                Assert.Equal(3, executionMethodInvoked);
                Assert.Equal(2, sleepTimeProviderInvoked);
                Assert.Equal(3, opResult);
            }

            [Fact]
            public async Task NullExecutionDoesNothandleAnything()
            {
                // Arrange
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;
                int retryCount = 3;

                // Act
                var opResult = await HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                       null,
                       null).
                       ExecuteWithWaitAndRetryAsync(retryCount,
                       (currentCount) =>
                       {
                           Interlocked.Increment(ref sleepTimeProviderInvoked);
                           return TimeSpan.FromMilliseconds(1);
                       },
                       () =>
                       {
                           return Task<int>.Run(() =>
                           {
                               return Interlocked.Increment(ref executionMethodInvoked);
                           });
                       });

                // Assert
                Assert.Equal(1, executionMethodInvoked);
                Assert.Equal(0, sleepTimeProviderInvoked);
                Assert.Equal(1, opResult);
            }

            [Fact]
            public async Task FalseExecutionDoesNothandleAnything()
            {
                // Arrange
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;
                int retryCount = 3;

                // Act
                var opResult = await HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                       null,
                       result => false).
                       ExecuteWithWaitAndRetryAsync(retryCount,
                       (currentCount) =>
                       {
                           Interlocked.Increment(ref sleepTimeProviderInvoked);
                           return TimeSpan.FromMilliseconds(1);
                       },
                       () =>
                       {
                           return Task<int>.Run(() =>
                           {
                               return Interlocked.Increment(ref executionMethodInvoked);
                           });
                       });

                // Assert
                Assert.Equal(1, executionMethodInvoked);
                Assert.Equal(0, sleepTimeProviderInvoked);
                Assert.Equal(1, opResult);
            }

            [Theory]
            [InlineData(0)]
            [InlineData(1)]
            [InlineData(5)]
            public async Task TrueExecutionHandlesEverything(int retryCount)
            {
                // Arrange
                int sleepTimeProviderInvoked = 0;
                int executionMethodInvoked = 0;

                // Act
                var opResult = await HttpRetryMessageHandler.RetryPolicy<int>.HandleExceptionAndResult(
                       null,
                       result => true).
                       ExecuteWithWaitAndRetryAsync(retryCount,
                       (currentCount) =>
                       {
                           Interlocked.Increment(ref sleepTimeProviderInvoked);
                           return TimeSpan.FromMilliseconds(1);
                       },
                       () =>
                       {
                           return Task<int>.Run(() =>
                           {
                               return Interlocked.Increment(ref executionMethodInvoked);
                           });
                       });

                // Assert
                Assert.Equal(retryCount+1, executionMethodInvoked);
                Assert.Equal(retryCount, sleepTimeProviderInvoked);
                Assert.Equal(retryCount + 1, opResult);
            }
        }
    }
}
