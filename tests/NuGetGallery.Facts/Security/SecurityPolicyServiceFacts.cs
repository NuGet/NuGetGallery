// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Web;
using Moq;
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Security
{
    public class SecurityPolicyServiceFacts
    {
        [Fact]
        public void EvaluateReturnsSuccessWithoutEvaluationIfNoPoliciesFound()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");

            // Act
            var result = service.Evaluate(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.MockPushPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Never);
            service.MockPushPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Never);
        }

        [Fact]
        public void EvaluateReturnsSuccessWithEvaluationIfPoliciesFoundAndMet()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            user.SecurityPolicies.Add(new UserSecurityPolicy("MockPushPolicy1"));
            user.SecurityPolicies.Add(new UserSecurityPolicy("MockPushPolicy2"));

            // Act
            var result = service.Evaluate(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.MockPushPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Once);
            service.MockPushPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Once);
        }

        [Fact]
        public void EvaluateReturnsAfterFirstFailure()
        {
            // Arrange
            var service = new TestSecurityPolicyService(success1: false, success2: true);
            var user = new User("testUser");
            user.SecurityPolicies.Add(new UserSecurityPolicy("MockPushPolicy1"));
            user.SecurityPolicies.Add(new UserSecurityPolicy("MockPushPolicy2"));

            // Act
            var result = service.Evaluate(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.False(result.Success);
            Assert.Equal("MockPushPolicy1", result.ErrorMessage);

            service.MockPushPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Once);
            service.MockPushPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Never);
        }

        [Fact]
        public void LoadUserPolicyHandlersPopulatesAllHandlers()
        {
            // Arrange & Act
            var handlers = new SecurityPolicyService().UserPolicyHandlers.ToList();

            // Assert
            Assert.NotNull(handlers);
            Assert.Equal(2, handlers.Count);
            Assert.Equal(typeof(RequireMinClientVersionForPushPolicy), handlers[0].GetType());
            Assert.Equal(typeof(RequirePackageVerifyScopePolicy), handlers[1].GetType());
        }

        private HttpContextBase CreateHttpContext(User user)
        {
            var httpContext = new Mock<HttpContextBase>();
            httpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object> { { "owin.Environment", new Dictionary<string, object>() } });

            var owinContext = httpContext.Object.GetOwinContext();
            owinContext.Environment[Constants.CurrentUserOwinEnvironmentKey] = user;
            owinContext.Request.User = Fakes.ToPrincipal(user);

            return httpContext.Object;
        }

        class TestSecurityPolicyService : SecurityPolicyService
        {
            public TestSecurityPolicyService(bool success1 = true, bool success2 = true)
            {
                MockPushPolicy1 = MockHandler("MockPushPolicy1", success1);
                MockPushPolicy2 = MockHandler("MockPushPolicy2", success2);
            }

            public Mock<UserSecurityPolicyHandler> MockPushPolicy1 { get; set; }

            public Mock<UserSecurityPolicyHandler> MockPushPolicy2 { get; set; }

            protected override IEnumerable<UserSecurityPolicyHandler> CreateUserPolicyHandlers()
            {
                yield return MockPushPolicy1.Object;
                yield return MockPushPolicy2.Object;
            }

            private Mock<UserSecurityPolicyHandler> MockHandler(string name, bool success)
            {
                var mock = new Mock<UserSecurityPolicyHandler>(name, SecurityPolicyAction.PackagePush);
                mock.Setup(m => m.Evaluate(It.IsAny<UserSecurityPolicyContext>()))
                    .Returns(new SecurityPolicyResult(success, name)).Verifiable();
                return mock;
            }
        }
    }
}
