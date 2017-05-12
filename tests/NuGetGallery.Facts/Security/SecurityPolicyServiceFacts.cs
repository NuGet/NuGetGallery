// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Diagnostics;
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Security
{
    public class SecurityPolicyServiceFacts
    {
        private static IEntitiesContext _entities = new Mock<IEntitiesContext>().Object;
        private static IAuditingService _auditing = new Mock<IAuditingService>().Object;
        private static IDiagnosticsService _diagnostics = new Mock<IDiagnosticsService>().Object;

        public static IEnumerable<object[]> CtorThrowNullReference_Data
        {
            get
            {
                yield return new object[] { null, _auditing, _diagnostics};
                yield return new object[] { _entities, null, _diagnostics };
                yield return new object[] { _entities, _auditing, null };
            }
        }
        
        [Theory]
        [MemberData(nameof(CtorThrowNullReference_Data))]
        public void CtorThrowsNullReferenceIfArgumentIsMissing(
            IEntitiesContext entities, IAuditingService auditing, IDiagnosticsService diagnostics)
        {
            Assert.Throws<ArgumentNullException>(() => new SecurityPolicyService(entities, auditing, diagnostics));
        }

        [Fact]
        public void UserHandlers()
        {
            // Arrange.
            var service = new SecurityPolicyService(_entities, _auditing, _diagnostics);

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
        public async void EvaluateThrowsIfHttpContextNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => new TestSecurityPolicyService()
                .EvaluateAsync(SecurityPolicyAction.PackagePush, null));
        }

        [Fact]
        public async void EvaluateReturnsSuccessWithoutEvaluationIfNoPoliciesFound()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");

            // Act
            var result = await service.EvaluateAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.Mocks.MockPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Never);
            service.Mocks.MockPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Never);
        }

        [Fact]
        public async void EvaluateReturnsSuccessWithEvaluationIfPoliciesFoundAndMet()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.Mocks.MockPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            service.Mocks.MockPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
        }

        [Fact]
        public async void EvaluateReturnsAfterFirstFailure()
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: false, policy2Result: true);
            var service = new TestSecurityPolicyService(policyData);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            service.Mocks.VerifyPolicyEvaluation(expectedPolicy1: false, expectedPolicy2: null, actual: result);
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 1)]
        public async void EvaluateSavesAuditRecordIfWasSuccessOrFailure(bool success, int times)
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: success, policy2Result: success);
            var service = new TestSecurityPolicyService(policyData);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.Equal(success, result.Success);
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Exactly(times));
        }

        [Fact]
        public void IsSubscribedThrowsIfUserNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestSecurityPolicyService().IsSubscribed(null, new Mock<IUserSecurityPolicySubscription>().Object));
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
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

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
            var subscription = service.Mocks.Subscription.Object;
            foreach (var policy in subscription.Policies)
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
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies.Add(subscription.Policies.First());

            // Act & Assert.
            Assert.False(service.IsSubscribed(user, service.UserSubscriptions.First()));
        }

        [Fact]
        public void SubscribeThrowsIfUserNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(null, new Mock<IUserSecurityPolicySubscription>().Object));
        }

        [Fact]
        public void SubscribeThrowsIfSubscriptionNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(new User(), null));
        }

        [Fact]
        public async void SubscribeAddsUserPoliciesWhenNone()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");

            // Act.
            await service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(2, user.SecurityPolicies.Count);
            service.Mocks.VerifySubscriptionPolicies(user.SecurityPolicies);

            service.Mocks.Subscription.Verify(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async void SubscribeAddsUserPoliciesWhenSameFromDifferentSubscription()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscriptionName2 = "MockSubscription2";
            var subscription = service.Mocks.Subscription.Object;
            foreach (var policy in subscription.Policies)
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, subscriptionName2));
            }

            // Act.
            await service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            var policies = user.SecurityPolicies.ToList();
            Assert.Equal(4, policies.Count);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            service.Mocks.VerifySubscriptionPolicies(policies.Skip(2));

            service.Mocks.Subscription.Verify(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async void SubscribeSkipsUserPoliciesWhenAlreadySubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            foreach (var policy in subscription.Policies)
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
            }
            Assert.Equal(2, user.SecurityPolicies.Count);

            // Act.
            await service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(2, user.SecurityPolicies.Count);
            service.Mocks.VerifySubscriptionPolicies(user.SecurityPolicies);

            service.Mocks.Subscription.Verify(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Never);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async void SubscribeSavesAuditRecordIfWasNotSubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.UserSubscriptions.First();

            // Act.
            await service.SubscribeAsync(user, subscription);

            // Act & Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Once);
        }

        [Fact]
        public async void SubscribeDoesNotSaveAuditRecordIfWasSubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.UserSubscriptions.First();
            await service.SubscribeAsync(user, subscription);
            service.MockAuditingService.ResetCalls();

            // Act.
            await service.SubscribeAsync(user, subscription);

            // Act & Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
        }

        [Fact]
        public void UnsubscribeThrowsIfUserNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(null, new Mock<IUserSecurityPolicySubscription>().Object));
        }

        [Fact]
        public void UnsubscribeThrowsIfSubscriptionNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(new User(), null));
        }

        [Fact]
        public async void UnsubscribeRemovesAllSubscriptionPolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            foreach (var policy in subscription.Policies)
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
            }
            Assert.Equal(2, user.SecurityPolicies.Count);

            // Act.
            await service.UnsubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(0, user.SecurityPolicies.Count);

            service.Mocks.Subscription.Verify(s => s.OnUnsubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            service.MockUserSecurityPolicies.Verify(p => p.Remove(It.IsAny<UserSecurityPolicy>()), Times.Exactly(2));
        }

        [Fact]
        public async void UnsubscribeDoesNotRemoveOtherSubscriptionPolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscriptionName2 = "MockSubscription2";
            var subscription = service.Mocks.Subscription.Object;
            foreach (var policy in subscription.Policies)
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy));
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, subscriptionName2));
            }
            Assert.Equal(4, user.SecurityPolicies.Count);

            // Act.
            await service.UnsubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            var policies = user.SecurityPolicies.ToList();
            Assert.Equal(2, policies.Count);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            Assert.Equal(subscriptionName2, policies[1].Subscription);

            service.Mocks.Subscription.Verify(s => s.OnUnsubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            service.MockUserSecurityPolicies.Verify(p => p.Remove(It.IsAny<UserSecurityPolicy>()), Times.Exactly(2));
        }

        [Fact]
        public async void UnsubscribeRemovesNoneIfNotSubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscriptionName2 = "MockSubscription2";
            var subscription = service.Mocks.Subscription.Object;
            foreach (var policy in subscription.Policies)
            {
                user.SecurityPolicies.Add(new UserSecurityPolicy(policy.Name, subscriptionName2));
            }
            Assert.Equal(2, user.SecurityPolicies.Count);

            // Act.
            await service.UnsubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.Equal(2, user.SecurityPolicies.Count);

            service.Mocks.Subscription.Verify(s => s.OnUnsubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Never);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            service.MockUserSecurityPolicies.Verify(p => p.Remove(It.IsAny<UserSecurityPolicy>()), Times.Never);
        }

        [Fact]
        public async void UnsubscribeSavesAuditRecordIfWasSubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.UserSubscriptions.First();
            await service.SubscribeAsync(user, subscription);
            service.MockAuditingService.ResetCalls();

            // Act.
            await service.UnsubscribeAsync(user, subscription);

            // Act & Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Once);
        }

        [Fact]
        public async void UnsubscribeDoesNotSaveAuditRecordIfWasNotSubscribed()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.UserSubscriptions.First();

            // Act.
            await service.UnsubscribeAsync(user, subscription);

            // Act & Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
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
    }
}
