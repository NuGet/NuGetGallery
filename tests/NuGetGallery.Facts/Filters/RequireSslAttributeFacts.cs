// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Filters
{
    public class RequireSslAttributeFacts
    {
        [Fact]
        public void RequireHttpsAttributeDoesNotThrowWhenRequireSSLIsFalse()
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            var context = mockAuthContext.Object;

            var attribute = new RequireSslAttribute() { Configuration = mockConfig.Object };
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.Same(result, context.Result);
        }

        [Fact]
        public void RequireHttpsAttributeDoesNotThrowForSecureConnection()
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(true);
            var context = mockAuthContext.Object;
            var attribute = new RequireSslAttribute() { Configuration = mockConfig.Object };
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.Same(result, context.Result);
        }

        [Theory]
        [InlineData(443, "{0}")]            // Authenticated, always force SSL for this action
        [InlineData(44300, "{0}:44300")]    // Non-standard Port, Authenticated, always force SSL for this action
        public void RequireHttpsAttributeRedirectsGetRequest(int port, string hostFormatter)
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();

            mockAuthContext.SetupGet(c => c.HttpContext.Request.HttpMethod).Returns("get");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.Url).Returns(new Uri("http://test.nuget.org/login"));
            mockAuthContext.SetupGet(c => c.HttpContext.Request.RawUrl).Returns("/login");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);

            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            mockConfig.Setup(cfg => cfg.SSLPort).Returns(port);
            
            var attribute = new RequireSslAttribute()
            {
                Configuration = mockConfig.Object
            };

            var result = new ViewResult();
            var context = mockAuthContext.Object;
            
            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.IsType<RedirectResult>(context.Result);
            Assert.Equal("https://" + String.Format(hostFormatter, "test.nuget.org") + "/login", ((RedirectResult)context.Result).Url);
        }

        
        // Each set permutes HTTP Methods for the same parameters as the test above
        [Theory]
        [InlineData("POST")]
        [InlineData("DELETE")]
        [InlineData("PUT")]
        [InlineData("HEAD")]
        [InlineData("TRACE")]
        public void RequireHttpsAttributeReturns403IfNonGetRequest(string method)
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();

            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.HttpMethod).Returns(method);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.Url).Returns(new Uri("http://test.nuget.org/api/create"));
            mockAuthContext.SetupGet(c => c.HttpContext.Request.RawUrl).Returns("/api/create");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);

            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            var context = mockAuthContext.Object;

            var attribute = new RequireSslAttribute()
            {
                Configuration = mockConfig.Object
            };

            // Act 
            attribute.OnAuthorization(context);

            // Assert
            Assert.IsType<HttpStatusCodeWithBodyResult>(context.Result);
            var result = (HttpStatusCodeWithBodyResult)context.Result;
            Assert.Equal(403, result.StatusCode);
            Assert.Equal("The requested resource can only be accessed via SSL.", result.StatusDescription);
        }
    }
}