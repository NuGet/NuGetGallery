using System;
using System.Web.Mvc;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class RequireRemoteHttpsAttributeFacts
    {
        [Fact]
        public void RequireFactsAttributeDoesNotThrowForLocalHostRequests()
        {
            // Arrange
            Mock<AuthorizationContext> mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(true);
            var context = mockAuthContext.Object;
            var attribute = new RequireRemoteHttpsAttribute();
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
            Mock<AuthorizationContext> mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(true);
            var context = mockAuthContext.Object;
            var attribute = new RequireRemoteHttpsAttribute();
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.Same(result, context.Result);
        }

        [Fact]
        public void RequireHttpsAttributeRedirectsGetRequest()
        {
            // Arrange
            Mock<AuthorizationContext> mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.HttpMethod).Returns("get");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.Url).Returns(new Uri("http://test.nuget.org/login"));
            mockAuthContext.SetupGet(c => c.HttpContext.Request.RawUrl).Returns("/login");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            var context = mockAuthContext.Object;
            var attribute = new RequireRemoteHttpsAttribute();
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.IsType<RedirectResult>(context.Result);
            Assert.Equal("https://test.nuget.org/login", ((RedirectResult)context.Result).Url);
        }

        [Theory]
        [InlineData(new object[] { "POST" })]
        [InlineData(new object[] { "DELETE" })]
        [InlineData(new object[] { "PUT" })]
        [InlineData(new object[] { "head" })]
        [InlineData(new object[] { "trace" })]
        public void RequireHttpsAttributeReturns403IfNonGetRequest(string method)
        {
            // Arrange
            Mock<AuthorizationContext> mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.HttpMethod).Returns(method);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.Url).Returns(new Uri("http://test.nuget.org/api/create"));
            mockAuthContext.SetupGet(c => c.HttpContext.Request.RawUrl).Returns("/api/create");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            var context = mockAuthContext.Object;
            var attribute = new RequireRemoteHttpsAttribute();

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
