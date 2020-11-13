// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using Xunit;

namespace NuGet.Services.SearchService
{
    public class ApiExceptionFilterAttributeFacts
    {
        [Fact]
        public void DoesNothingForUnknownException()
        {
            var target = new ApiExceptionFilterAttribute();
            var context = GetContext(new InvalidOperationException("Something weird."));

            target.OnException(context);

            Assert.Null(context.Result);
        }

        [Theory]
        [MemberData(nameof(KnownExceptions))]
        public void ReturnsExpectedStatusCodeForKnowExceptions(Exception ex, HttpStatusCode statusCode, string message)
        {
            var target = new ApiExceptionFilterAttribute();
            var context = GetContext(ex);

            target.OnException(context);

            Assert.NotNull(context.Result);
            var jsonResult = Assert.IsType<JsonResult>(context.Result);
            Assert.Equal(statusCode, (HttpStatusCode)jsonResult.StatusCode);
            var response = Assert.IsType<ErrorResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Equal(message, response.Message);
        }

        private static ExceptionContext GetContext(Exception ex)
        {
            return new ExceptionContext(new ActionContext(
                Mock.Of<HttpContext>(),
                new RouteData(),
                new ActionDescriptor()), new List<IFilterMetadata>())
            {
                Exception = ex,
            };
        }

        public static IEnumerable<object[]> KnownExceptions => new[]
        {
            new object[]
            {
                new AzureSearchException("Azure Search died!", null),
                HttpStatusCode.ServiceUnavailable,
                "The service is unavailable.",
            },
            new object[]
            {
                new InvalidSearchRequestException("Bad!"),
                HttpStatusCode.BadRequest,
                "Bad!",
            },
        };
    }
}
