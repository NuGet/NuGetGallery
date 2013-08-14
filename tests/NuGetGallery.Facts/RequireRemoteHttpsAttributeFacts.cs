using System;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class RequireRemoteHttpsAttributeFacts
    {
        [Fact]
        public void RequireHttpsAttributeDoesNotThrowWhenRequireSSLIsFalse()
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();
            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            var context = mockAuthContext.Object;

            mockFormsAuth.Setup(fas => fas.ShouldForceSSL(context.HttpContext)).Returns(true);

            var attribute = new RequireRemoteHttpsAttribute() { Configuration = mockConfig.Object, FormsAuthentication = mockFormsAuth.Object };
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
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();
            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(true);
            var context = mockAuthContext.Object;
            mockFormsAuth.Setup(fas => fas.ShouldForceSSL(context.HttpContext)).Returns(true);
            var attribute = new RequireRemoteHttpsAttribute() { Configuration = mockConfig.Object, FormsAuthentication = mockFormsAuth.Object };
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.Same(result, context.Result);
        }

        [Fact]
        public void RequireHttpsAttributeDoesNotThrowForInsecureConnectionIfNotAuthenticatedOrForcingSSLAndOnlyWhenAuthenticatedSet()
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();
            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsAuthenticated).Returns(false);
            var context = mockAuthContext.Object;
            mockFormsAuth.Setup(fas => fas.ShouldForceSSL(context.HttpContext)).Returns(false);
            var attribute = new RequireRemoteHttpsAttribute()
            {
                Configuration = mockConfig.Object,
                OnlyWhenAuthenticated = true,
                FormsAuthentication = mockFormsAuth.Object
            };
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.Same(result, context.Result);
        }

        [Theory]
        [InlineData(true, false, false, 443, "{0}")]            // Authenticated, always force SSL for this action
        [InlineData(false, false, false, 443, "{0}")]           // Not Authenticated, always force SSL for this action
        [InlineData(true, false, true, 443, "{0}")]             // Authenticated, only force SSL if already authenticated
        [InlineData(false, true, true, 443, "{0}")]             // Authenticated, should be authenticated, force SSL
        [InlineData(true, false, false, 44300, "{0}:44300")]    // Non-standard Port, Authenticated, always force SSL for this action
        [InlineData(false, false, false, 44300, "{0}:44300")]   // Non-standard Port, Not Authenticated, always force SSL for this action
        [InlineData(true, false, true, 44300, "{0}:44300")]     // Non-standard Port, Authenticated, only force SSL if already authenticated
        [InlineData(false, true, true, 44300, "{0}:44300")]     // Non-standard Port, Authenticated, should be authenticated, force SSL
        public void RequireHttpsAttributeRedirectsGetRequest(bool isAuthenticated, bool forceSSL, bool onlyWhenAuthenticated, int port, string hostFormatter)
        {
            // Arrange
            var mockAuthContext = new Mock<AuthorizationContext>(MockBehavior.Strict);
            var mockConfig = new Mock<IAppConfiguration>();
            var mockFormsAuth = new Mock<IFormsAuthenticationService>();

            mockAuthContext.SetupGet(c => c.HttpContext.Request.HttpMethod).Returns("get");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.Url).Returns(new Uri("http://test.nuget.org/login"));
            mockAuthContext.SetupGet(c => c.HttpContext.Request.RawUrl).Returns("/login");
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsSecureConnection).Returns(false);
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsAuthenticated).Returns(isAuthenticated);

            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            mockConfig.Setup(cfg => cfg.SSLPort).Returns(port);
            
            var context = mockAuthContext.Object;
            mockFormsAuth.Setup(fas => fas.ShouldForceSSL(context.HttpContext)).Returns(forceSSL);

            var attribute = new RequireRemoteHttpsAttribute()
            {
                Configuration = mockConfig.Object,
                OnlyWhenAuthenticated = onlyWhenAuthenticated,
                FormsAuthentication = mockFormsAuth.Object
            };
            var result = new ViewResult();
            context.Result = result;

            // Act
            attribute.OnAuthorization(context);

            // Assert
            Assert.IsType<RedirectResult>(context.Result);
            Assert.Equal("https://" + String.Format(hostFormatter, "test.nuget.org") + "/login", ((RedirectResult)context.Result).Url);
        }

        
        // Each set permutes HTTP Methods for the same parameters as the test above
        [Theory]
        [InlineData("POST", true, false, false)]
        [InlineData("DELETE", true, false, false)]
        [InlineData("PUT", true, false, false)]
        [InlineData("HEAD", true, false, false)]
        [InlineData("TRACE", true, false, false)]

        [InlineData("POST", false, false, false)]
        [InlineData("DELETE", false, false, false)]
        [InlineData("PUT", false, false, false)]
        [InlineData("HEAD", false, false, false)]
        [InlineData("TRACE", false, false, false)]

        [InlineData("POST", true, false, true)]
        [InlineData("DELETE", true, false, true)]
        [InlineData("PUT", true, false, true)]
        [InlineData("HEAD", true, false, true)]
        [InlineData("TRACE", true, false, true)]

        [InlineData("POST", false, true, true)]
        [InlineData("DELETE", false, true, true)]
        [InlineData("PUT", false, true, true)]
        [InlineData("HEAD", false, true, true)]
        [InlineData("TRACE", false, true, true)]
        public void RequireHttpsAttributeReturns403IfNonGetRequest(string method, bool isAuthenticated, bool forceSSL, bool onlyWhenAuthenticated)
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
            mockAuthContext.SetupGet(c => c.HttpContext.Request.IsAuthenticated).Returns(isAuthenticated);

            mockConfig.Setup(cfg => cfg.RequireSSL).Returns(true);
            var context = mockAuthContext.Object;

            mockFormsAuth.Setup(fas => fas.ShouldForceSSL(context.HttpContext)).Returns(forceSSL);

            var attribute = new RequireRemoteHttpsAttribute()
            {
                Configuration = mockConfig.Object,
                OnlyWhenAuthenticated = onlyWhenAuthenticated,
                FormsAuthentication = mockFormsAuth.Object
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