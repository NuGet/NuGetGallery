using System;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class RequireHttpsNoLocalAttributeFacts
    {
        [Fact]
        public void RequireFactsAttributeDoesNotThrowForLocalHostRequests()
        {
            // Arrange
            Mock<AuthorizationContext> mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(true);
            var context = mockAuthContext.Object;
            var attribute = new RequireHttpsNoLocalAttribute();
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
            var attribute = new RequireHttpsNoLocalAttribute();
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
            var attribute = new RequireHttpsNoLocalAttribute();
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.IsType<RedirectResult>(context.Result);
            Assert.Equal("https://test.nuget.org/login", ((RedirectResult)context.Result).Url);
        }

        [Fact]
        public void RequireHttpsAttributeThrowsIfPostRequest()
        {
            // Arrange
            Mock<AuthorizationContext> mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsLocal).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.HttpMethod).Returns("POST");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.Url).Returns(new Uri("http://test.nuget.org/api/create"));
            mockAuthContext.SetupGet(c => c.HttpContext.Request.RawUrl).Returns("/api/create");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            var context = mockAuthContext.Object;
            var attribute = new RequireHttpsNoLocalAttribute();

            // Act 
            Exception exception = Record.Exception(() => attribute.OnAuthorization(context));

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal("The requested resource can only be accessed via SSL.", exception.Message);
        }
    }
}
