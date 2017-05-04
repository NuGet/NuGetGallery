// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        public void CtorThrowsIfEntitiesContextNull()
        {
            Assert.Throws<ArgumentNullException>(() => new SecurityPolicyService(null));
        }

        [Fact]
        public void UserSubscriptions()
        {
            // Arrange.
            var entitiesContext = new Mock<IEntitiesContext>();
            var service = new SecurityPolicyService(entitiesContext.Object);

            // Act.
            var subscriptions = service.UserSubscriptions;

            // Assert.
            Assert.Equal(1, subscriptions.Count());
            Assert.Equal("SecurePush", subscriptions.First().Name);
        }

        [Fact]
        public void UserHandlers()
        {
            // Arrange.
            var entitiesContext = new Mock<IEntitiesContext>();
            var service = new SecurityPolicyService(entitiesContext.Object);

            // Act.
            var handlers = ((IEnumerable<UserSecurityPolicyHandler>)service.GetType()
                .GetProperty("UserHandlers", BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(service)).ToList();

            // Assert
            Assert.NotNull(handlers);
            Assert.Equal(2, handlers.Count);
            Assert.Equal(typeof(RequireMinClientVersionForPushPolicy), handlers[0].GetType());
            Assert.Equal(typeof(RequirePackageVerifyScopePolicy), handlers[1].GetType());
        }

        [Fact]
        public void EvaluateThrowsIfHttpContextNull()
        {
            Assert.Throws<ArgumentNullException>(() => new TestSecurityPolicyService()
                .Evaluate(SecurityPolicyAction.PackagePush, null));
        }

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

            service.MockPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Never);
            service.MockPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Never);
        }

        [Fact]
        public void EvaluateReturnsSuccessWithEvaluationIfPoliciesFoundAndMet()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            user.SecurityPolicies = TestSecurityPolicyService.GetMockPolicies().ToList();

            // Act
            var result = service.Evaluate(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.MockPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Once);
            service.MockPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Once);
        }

        [Fact]
        public void EvaluateReturnsAfterFirstFailure()
        {
            // Arrange
            var service = new TestSecurityPolicyService(success1: false, success2: true);
            var user = new User("testUser");
            user.SecurityPolicies = TestSecurityPolicyService.GetMockPolicies().ToList();

            // Act
            var result = service.Evaluate(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.False(result.Success);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy1), result.ErrorMessage);

            service.MockPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Once);
            service.MockPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyContext>()), Times.Never);
        }

        [Fact]
        public void IsSubscribedThrowsIfUserNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestSecurityPolicyService().IsSubscribed(null, new SecurePushSubscription()));
        }

        [Fact]
        public void IsSubscribedThrowsIfSubscriptionNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestSecurityPolicyService().IsSubscribed(new User(), null));
        }

        [Fact]
        public void IsSubscribedReturnsTrueWhenUserHasSamePolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            user.SecurityPolicies = TestSecurityPolicyService.GetMockPolicies().ToList();

            // Act & Assert.
            Assert.True(service.IsSubscribed(user, service.UserSubscriptions.First()));
        }

        [Fact]
        public void IsSubscribedReturnsTrueWhenUserHasMorePolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            user.SecurityPolicies.Add(new UserSecurityPolicy("OtherPolicy", "OtherSubscription"));
            foreach (var policy in TestSecurityPolicyService.GetMockPolicies())
            {
                user.SecurityPolicies.Add(policy);
            }

            // Act & Assert.
            Assert.True(service.IsSubscribed(user, service.UserSubscriptions.First()));
        }

        [Fact]
        public void IsSubscribedReturnsTrueWhenUserDoesNotHaveAllPolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            user.SecurityPolicies.Add(TestSecurityPolicyService.GetMockPolicies().First());

            // Act & Assert.
            Assert.False(service.IsSubscribed(user, service.UserSubscriptions.First()));
        }

        [Fact]
        public void SubscribeThrowsIfUserNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(null, new SecurePushSubscription()));
        }

        [Fact]
        public void SubscribeThrowsIfSubscriptionNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(new User(), null));
        }

        [Fact]
        public void SubscribeAddsUserPoliciesWhenNone()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");

            // Act.
            service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            var policies = user.SecurityPolicies.ToList();
            Assert.Equal(2, policies.Count);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy1), policies[0].Name);
            Assert.Equal(TestSecurityPolicyService.MockSubscriptionName, policies[0].Subscription);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy2), policies[1].Name);
            Assert.Equal(TestSecurityPolicyService.MockSubscriptionName, policies[1].Subscription);

            service.MockSubscription.Verify(s => s.OnSubscribe(It.IsAny<User>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public void SubscribeAddsUserPoliciesWhenSameFromDifferentSubscription()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscriptionName2 = "MockSubscription2";
            foreach (var policy in TestSecurityPolicyService.GetMockPolicies())
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, subscriptionName2));
            }

            // Act.
            service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            var policies = user.SecurityPolicies.ToList();
            Assert.Equal(4, policies.Count);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy1), policies[0].Name);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy2), policies[1].Name);
            Assert.Equal(subscriptionName2, policies[1].Subscription);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy1), policies[2].Name);
            Assert.Equal(TestSecurityPolicyService.MockSubscriptionName, policies[2].Subscription);
            Assert.Equal(nameof(TestSecurityPolicyService.MockPolicy2), policies[3].Name);
            Assert.Equal(TestSecurityPolicyService.MockSubscriptionName, policies[3].Subscription);

            service.MockSubscription.Verify(s => s.OnSubscribe(It.IsAny<User>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public void SubscribeSkipsUserPoliciesWhenAlreadySubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            foreach (var policy in TestSecurityPolicyService.GetMockPolicies())
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
            }
            Assert.Equal(2, user.SecurityPolicies.Count);

            // Act.
            service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(2, user.SecurityPolicies.Count);

            service.MockSubscription.Verify(s => s.OnSubscribe(It.IsAny<User>()), Times.Never);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public void UnsubscribeThrowsIfUserNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(null, new SecurePushSubscription()));
        }

        [Fact]
        public void UnsubscribeThrowsIfSubscriptionNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(new User(), null));
        }

        [Fact]
        public void UnsubscribeRemovesAllSubscriptionPolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            foreach (var policy in TestSecurityPolicyService.GetMockPolicies())
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
            }
            Assert.Equal(2, user.SecurityPolicies.Count);

            // Act.
            service.UnsubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(0, user.SecurityPolicies.Count);

            service.MockSubscription.Verify(s => s.OnUnsubscribe(It.IsAny<User>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            service.MockUserSecurityPolicies.Verify(p => p.Remove(It.IsAny<UserSecurityPolicy>()), Times.Exactly(2));
        }

        [Fact]
        public void UnsubscribeDoesNotRemoveOtherSubscriptionPolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscriptionName2 = "MockSubscription2";
            foreach (var policy in TestSecurityPolicyService.GetMockPolicies())
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, subscriptionName2));
            }
            Assert.Equal(4, user.SecurityPolicies.Count);

            // Act.
            service.UnsubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            var policies = user.SecurityPolicies.ToList();
            Assert.Equal(2, policies.Count);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            Assert.Equal(subscriptionName2, policies[1].Subscription);

            service.MockSubscription.Verify(s => s.OnUnsubscribe(It.IsAny<User>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            service.MockUserSecurityPolicies.Verify(p => p.Remove(It.IsAny<UserSecurityPolicy>()), Times.Exactly(2));
        }

        [Fact]
        public void UnsubscribeRemovesNoneIfNotSubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscriptionName2 = "MockSubscription2";
            foreach (var policy in TestSecurityPolicyService.GetMockPolicies())
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, subscriptionName2));
            }
            Assert.Equal(2, user.SecurityPolicies.Count);

            // Act.
            service.UnsubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(2, user.SecurityPolicies.Count);

            service.MockSubscription.Verify(s => s.OnUnsubscribe(It.IsAny<User>()), Times.Never);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            service.MockUserSecurityPolicies.Verify(p => p.Remove(It.IsAny<UserSecurityPolicy>()), Times.Never);
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

        public class TestSecurityPolicyService : SecurityPolicyService
        {
            public const string MockSubscriptionName = "MockSubscription";

            public Mock<IEntitiesContext> MockEntitiesContext { get; }

            public Mock<IDbSet<UserSecurityPolicy>> MockUserSecurityPolicies { get; set; }

            public Mock<IUserSecurityPolicySubscription> MockSubscription { get; }

            public Mock<UserSecurityPolicyHandler> MockPolicy1 { get; }

            public Mock<UserSecurityPolicyHandler> MockPolicy2 { get; }

            public TestSecurityPolicyService(bool success1 = true, bool success2 = true)
            {
                MockUserSecurityPolicies = new Mock<IDbSet<UserSecurityPolicy>>();
                MockUserSecurityPolicies.Setup(p => p.Remove(It.IsAny<UserSecurityPolicy>())).Verifiable();

                MockEntitiesContext = new Mock<IEntitiesContext>();
                MockEntitiesContext.Setup(c => c.SaveChangesAsync()).Returns(Task.FromResult(2)).Verifiable();
                MockEntitiesContext.Setup(c => c.UserSecurityPolicies).Returns(MockUserSecurityPolicies.Object);
                EntitiesContext = MockEntitiesContext.Object;

                MockPolicy1 = MockHandler(nameof(MockPolicy1), success1);
                MockPolicy2 = MockHandler(nameof(MockPolicy2), success2);

                MockSubscription = new Mock<IUserSecurityPolicySubscription>();
                MockSubscription.Setup(s => s.Name).Returns(MockSubscriptionName);
                MockSubscription.Setup(s => s.OnSubscribe(It.IsAny<User>())).Verifiable();
                MockSubscription.Setup(s => s.OnUnsubscribe(It.IsAny<User>())).Verifiable();
                MockSubscription.Setup(s => s.Policies).Returns(GetMockPolicies());
            }

            public override IEnumerable<IUserSecurityPolicySubscription> UserSubscriptions
            {
                get
                {
                    yield return MockSubscription.Object;
                }
            }

            protected override IEnumerable<UserSecurityPolicyHandler> UserHandlers
            {
                get
                {
                    yield return MockPolicy1.Object;
                    yield return MockPolicy2.Object;
                }
            }

            public static IEnumerable<UserSecurityPolicy> GetMockPolicies()
            {
                yield return new UserSecurityPolicy(nameof(MockPolicy1), MockSubscriptionName);
                yield return new UserSecurityPolicy(nameof(MockPolicy2), MockSubscriptionName);
            }

            private Mock<UserSecurityPolicyHandler> MockHandler(string name, bool success)
            {
                var result = success ? SecurityPolicyResult.SuccessResult : SecurityPolicyResult.CreateErrorResult(name);
                var mock = new Mock<UserSecurityPolicyHandler>(name, SecurityPolicyAction.PackagePush);
                mock.Setup(m => m.Evaluate(It.IsAny<UserSecurityPolicyContext>())).Returns(result).Verifiable();
                return mock;
            }
        }
    }
}
