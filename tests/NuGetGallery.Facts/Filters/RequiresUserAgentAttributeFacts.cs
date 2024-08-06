// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Filters
{
    public class RequiresUserAgentAttributeFacts : TestContainer
    {
        [Fact]
        public void RequiresUserAgentHeader()
        {
            // Arrange
            var headers = new NameValueCollection();

            // Act
            Mock<ActionExecutingContext> mockActionContext = ExecuteWithHeaders(headers);

            // Assert
            var result = Assert.IsType<HttpStatusCodeWithBodyResult>(mockActionContext.Object.Result);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("User-Agent header is required", result.StatusDescription);
            Assert.Equal("A User-Agent header is required for this endpoint.", result.Body);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void RejectsEmptyUserAgentHeader(string value)
        {
            // Arrange
            var headers = new NameValueCollection
            {
                { "User-Agent", value }
            };

            // Act
            Mock<ActionExecutingContext> mockActionContext = ExecuteWithHeaders(headers);

            // Assert
            var result = Assert.IsType<HttpStatusCodeWithBodyResult>(mockActionContext.Object.Result);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("User-Agent header is required", result.StatusDescription);
            Assert.Equal("A User-Agent header is required for this endpoint.", result.Body);
        }

        [Fact]
        public void AllowsRequestsWithUserAgent()
        {
            // Arrange
            var headers = new NameValueCollection
            {
                { "User-Agent", "Mozilla/5.0" }
            };

            // Act
            Mock<ActionExecutingContext> mockActionContext = ExecuteWithHeaders(headers);

            // Assert
            Assert.Null(mockActionContext.Object.Result);
        }

        private static Mock<ActionExecutingContext> ExecuteWithHeaders(NameValueCollection headers)
        {
            var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            httpRequest.SetupGet(r => r.Headers).Returns(headers);

            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

            var mockActionContext = new Mock<ActionExecutingContext>(MockBehavior.Strict);
            mockActionContext.SetupGet(x => x.HttpContext).Returns(httpContext.Object);

            // Act
            new RequiresUserAgentAttribute().OnActionExecuting(mockActionContext.Object);
            return mockActionContext;
        }
    }
}