using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Glimpse.Core.Extensibility;
using Moq;
using Xunit;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseRuntimePolicyFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void UsesConfigDefaultPolicyIfUserNotLoggedInAndNoCookie()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(false);
                var policyContext = new Mock<IRuntimePolicyContext>();
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.UserGlimpsePolicy)
                    .Returns(RuntimePolicy.ModifyResponseBody);

                // Act/Assert
                Assert.Equal(RuntimePolicy.ModifyResponseBody, policy.Execute(policyContext.Object, context.Object));
            }

            [Fact]
            public void UsesConfigDefaultPolicyIfSSLRequiredAndConnectionIsNotSecureAndNoCookie()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(false);
                var policyContext = new Mock<IRuntimePolicyContext>();
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);
                policy.MockConfiguration
                    .Setup(c => c.UserGlimpsePolicy)
                    .Returns(RuntimePolicy.ModifyResponseBody);

                // Act/Assert
                Assert.Equal(RuntimePolicy.ModifyResponseBody, policy.Execute(policyContext.Object, context.Object));
            }

            [Fact]
            public void UsesConfigDefaultPolicyIfUserIsNotAdminAndNoCookie()
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(true);
                context.Setup(c => c.User.IsInRole(Constants.AdminRoleName))
                       .Returns(false);
                var policyContext = new Mock<IRuntimePolicyContext>();
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);
                policy.MockConfiguration
                    .Setup(c => c.UserGlimpsePolicy)
                    .Returns(RuntimePolicy.ModifyResponseBody);

                // Act/Assert
                Assert.Equal(RuntimePolicy.ModifyResponseBody, policy.Execute(policyContext.Object, context.Object));
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
                var policyContext = new Mock<IRuntimePolicyContext>();
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);
                policy.MockConfiguration
                    .Setup(c => c.UserGlimpsePolicy)
                    .Returns(RuntimePolicy.ModifyResponseBody);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(policyContext.Object, context.Object));
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
                var policyContext = new Mock<IRuntimePolicyContext>();
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(false);
                policy.MockConfiguration
                    .Setup(c => c.UserGlimpsePolicy)
                    .Returns(RuntimePolicy.ModifyResponseBody);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(policyContext.Object, context.Object));
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
                var policyContext = new Mock<IRuntimePolicyContext>();
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);
                policy.MockConfiguration
                    .Setup(c => c.UserGlimpsePolicy)
                    .Returns(RuntimePolicy.ModifyResponseBody);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(policyContext.Object, context.Object));
            }
        }

        public class TestableGlimpseRuntimePolicy : GlimpseRuntimePolicy
        {
            public Mock<IConfiguration> MockConfiguration { get; private set; }

            public TestableGlimpseRuntimePolicy()
            {
                Configuration = (MockConfiguration = new Mock<IConfiguration>()).Object;
            }
        }
    }
}
