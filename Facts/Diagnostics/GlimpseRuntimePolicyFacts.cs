using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Glimpse.Core.Extensibility;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseRuntimePolicyFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void DisablesGlimpseIfUserNotLoggedInAndNoCookie()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(false);
                var policy = new TestableGlimpseRuntimePolicy();
                
                // Act/Assert
                Assert.Equal(RuntimePolicy.Off, policy.Execute(context.Object));
            }

            [Fact]
            public void DisablesGlimpseIfSSLRequiredAndConnectionIsNotSecureAndNoCookie()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(false);
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);

                // Act/Assert
                Assert.Equal(RuntimePolicy.Off, policy.Execute(context.Object));
            }

            [Fact]
            public void DisablesGlimpseIfUserIsNotAdminAndNoCookie()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(true);
                context.Setup(c => c.User.IsInRole(Constants.AdminRoleName))
                       .Returns(false);
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);

                // Act/Assert
                Assert.Equal(RuntimePolicy.Off, policy.Execute(context.Object));
            }

            [Fact]
            public void EnablesGlimpseCompletelyIfUserIsAdmin()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(true);
                context.Setup(c => c.User.IsInRole(Constants.AdminRoleName))
                       .Returns(true);
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(context.Object));
            }

            [Fact]
            public void EnablesGlimpseCompletelyOverHTTPIfRequireSSLFalse()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(false);
                context.Setup(c => c.User.IsInRole(Constants.AdminRoleName))
                       .Returns(true);
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(false);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(context.Object));
            }

            [Fact]
            public void EnablesGlimpseCompletelyIfRequestIsLocal()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsLocal)
                       .Returns(true);
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(false);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(false);
                context.Setup(c => c.User.IsInRole(Constants.AdminRoleName))
                       .Returns(false);
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(context.Object));
            }
        }

        public class TestableGlimpseRuntimePolicy : GlimpseRuntimePolicy
        {
            public Mock<IAppConfiguration> MockConfiguration { get; private set; }

            public TestableGlimpseRuntimePolicy()
            {
                Configuration = (MockConfiguration = new Mock<IAppConfiguration>()).Object;
            }
        }
    }
}
