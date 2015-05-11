// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
using Xunit.Extensions;

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
                context.Setup(c => c.Request.Path)
                       .Returns("/api");
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
                context.Setup(c => c.Request.Path)
                       .Returns("/api");
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
                context.Setup(c => c.Request.Path)
                       .Returns("/api");
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
                context.Setup(c => c.Request.Path)
                       .Returns("/api");
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
                context.Setup(c => c.Request.Path)
                    .Returns("/api");
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(false);

                // Act/Assert
                Assert.Equal(RuntimePolicy.On, policy.Execute(context.Object));
            }

            [Fact]
            public void EnablesGlimpsePersistenceIfRequestIsLocal()
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
                context.Setup(c => c.Request.Path)
                    .Returns("/api");
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);

                // Act/Assert
                Assert.Equal(RuntimePolicy.PersistResults, policy.Execute(context.Object));
            }

            [Theory]
            [InlineData("public")]
            [InlineData("content")]
            [InlineData("scripts")]
            [InlineData("/public")]
            [InlineData("/content")]
            [InlineData("/scripts")]
            public void DisablesGlimpseIfPathIsKnownToBeStaticContent(string path)
            {
                // Arrange
                var context = new Mock<HttpContextBase>();
                context.Setup(c => c.Request.IsLocal)
                       .Returns(false);
                context.Setup(c => c.Request.IsAuthenticated)
                       .Returns(true);
                context.Setup(c => c.Request.IsSecureConnection)
                       .Returns(true);
                context.Setup(c => c.User.IsInRole(Constants.AdminRoleName))
                       .Returns(true);
                context.Setup(c => c.Request.Path)
                    .Returns(path);
                var policy = new TestableGlimpseRuntimePolicy();
                policy.MockConfiguration
                    .Setup(c => c.RequireSSL)
                    .Returns(true);

                // Act/Assert
                Assert.Equal(RuntimePolicy.Off, policy.Execute(context.Object));
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
