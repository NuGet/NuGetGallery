// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.Hosting;
using Moq;
using Newtonsoft.Json.Linq;
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

            Assert.Null(context.Response);
        }

        [Theory]
        [MemberData(nameof(KnownExceptions))]
        public async Task ReturnsExpectedStatusCodeForKnowExceptions(Exception ex, HttpStatusCode statusCode, string message)
        {
            var target = new ApiExceptionFilterAttribute();
            var context = GetContext(ex);

            target.OnException(context);

            Assert.NotNull(context.Response);
            Assert.Equal(statusCode, context.Response.StatusCode);
            var content = await context.Response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            Assert.Equal(new[] { "Success", "Message" }, json.Properties().Select(x => x.Name).ToArray());
            Assert.Equal(false, json["Success"]);
            Assert.Equal(message, json["Message"]);
        }

        private static HttpActionExecutedContext GetContext(Exception ex)
        {
            var httpControllerContext = new HttpControllerContext
            {
                Request = new HttpRequestMessage(HttpMethod.Get, "https://example/query")
                {
                    Properties =
                    {
                        { HttpPropertyKeys.HttpConfigurationKey, new HttpConfiguration() },
                    },
                },
            };
            var httpActionContext = new HttpActionContext(httpControllerContext, actionDescriptor: Mock.Of<HttpActionDescriptor>());
            var context = new HttpActionExecutedContext(httpActionContext, ex);
            return context;
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
