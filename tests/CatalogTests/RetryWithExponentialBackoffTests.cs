// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class RetryWithExponentialBackoffTests
    {
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
                HttpStatusCode.BadRequest,
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
