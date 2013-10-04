using System;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Filters
{
    public class ApiKeyAuthorizeAttributeFacts
    {
        [Fact]
        public void ApiKeyAuthorizeAttributeDoesNotThrowWhenRequireSSLIsFalse()
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();
            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            var context = mockAuthContext.Object;

            mockFormsAuth.Setup(fas => fas.ShouldForceSSL(context.HttpContext)).Returns(true);

            var attribute = new ApiKeyAuthorizeAttribute() { Configuration = mockConfig.Object, FormsAuthentication = mockFormsAuth.Object };
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.Same(result, context.Result);
        }
    }
}