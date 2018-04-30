﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Security
{
    public class SecurityPolicyServiceFacts
    {
        private static IEntitiesContext _entities = new Mock<IEntitiesContext>().Object;
        private static IAuditingService _auditing = new Mock<IAuditingService>().Object;
        private static IDiagnosticsService _diagnostics = new Mock<IDiagnosticsService>().Object;
        private static IAppConfiguration _configuration = new Mock<IAppConfiguration>().Object;

        public static IEnumerable<object[]> CtorThrowNullReference_Data
        {
            get
            {
                yield return new object[] { null, _auditing, _diagnostics, _configuration};
                yield return new object[] { _entities, null, _diagnostics, _configuration };
                yield return new object[] { _entities, _auditing, null, _configuration };
                yield return new object[] { _entities, _auditing, _diagnostics, null };
            }
        }
        
        [Theory]
        [MemberData(nameof(CtorThrowNullReference_Data))]
        public void Constructor_ThrowsArgumentNullIfArgumentMissing(
            IEntitiesContext entities, IAuditingService auditing, IDiagnosticsService diagnostics, IAppConfiguration configuration)
        {
            Assert.Throws<ArgumentNullException>(() => new SecurityPolicyService(entities, auditing, diagnostics, configuration));
        }

        [Fact]
        public void UserHandlers_ReturnsRegisteredUserSecurityPolicyHandlers()
        {
            // Arrange.
            var service = new SecurityPolicyService(_entities, _auditing, _diagnostics, _configuration);

            // Act.
            var handlers = ((IEnumerable<UserSecurityPolicyHandler>)service.GetType()
                .GetProperty("UserHandlers", BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(service)).ToList();

            // Assert
            Assert.NotNull(handlers);
            Assert.Equal(5, handlers.Count);
            Assert.Equal(typeof(RequirePackageVerifyScopePolicy), handlers[0].GetType());
            Assert.Equal(typeof(RequireMinProtocolVersionForPushPolicy), handlers[1].GetType());
            Assert.Equal(typeof(RequireOrganizationTenantPolicy), handlers[2].GetType());
            Assert.Equal(typeof(ControlRequiredSignerPolicy), handlers[3].GetType());
            Assert.Equal(typeof(AutomaticallyOverwriteRequiredSignerPolicy), handlers[4].GetType());
        }

        [Fact]
        public async Task EvaluateAsync_ThrowsArgumentNullIfHttpContextIsNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => new TestSecurityPolicyService()
                .EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, null));
        }

        [Fact]
        public async Task EvaluateAsync_ReturnsSuccessWithoutEvaluationIfNoPoliciesWereFound()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.Mocks.MockPolicyHandler1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Never);
            service.Mocks.MockPolicyHandler2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Never);
        }

        [Fact]
        public async Task EvaluateAsync_ReturnsSuccessWithEvaluationIfPoliciesFoundAndMet()
        {
            // Arrange
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.Mocks.MockPolicyHandler1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            service.Mocks.MockPolicyHandler2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
        }

        [Fact]
        public async Task EvaluateAsync_ReturnsNonSuccessAfterFirstFailure()
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: false, policy2Result: true);
            var service = new TestSecurityPolicyService(policyData);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            service.Mocks.VerifyPolicyEvaluation(expectedPolicy1: false, expectedPolicy2: null, actual: result);
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 1)]
        public async Task EvaluateAsync_SavesAuditRecordsForSuccessAndFailureCases(bool success, int times)
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: success, policy2Result: success);
            var service = new TestSecurityPolicyService(policyData);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.Equal(success, result.Success);
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Exactly(times));
        }

        [Fact]
        public async Task EvaluateAsync_EvaluatesOnlyPoliciesRelevantToTheAction()
        {
            // Arrange
            const string extraPolicyName = "ExtraPolicy";
            var extraPolicyHandlerMock = new Mock<UserSecurityPolicyHandler>(extraPolicyName, SecurityPolicyAction.ManagePackageOwners);

            var policyData = new TestUserSecurityPolicyData();
            var policyHandlers = new List<UserSecurityPolicyHandler>(policyData.Handlers.Select(x => x.Object));
            policyHandlers.Add(extraPolicyHandlerMock.Object);

            var service = new TestSecurityPolicyService(policyData, policyHandlers);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;

            var userSecurityPolicies = new List<UserSecurityPolicy>(subscription.Policies);
            userSecurityPolicies.Add(new UserSecurityPolicy(extraPolicyName, "ExtraSubscription"));
            user.SecurityPolicies = userSecurityPolicies;

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.Mocks.MockPolicyHandler1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            service.Mocks.MockPolicyHandler2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            extraPolicyHandlerMock.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Never);
        }

        [Fact]
        public async Task EvaluateAsync_WhenEnforceDefaultSecurityPoliciesIsFalseDefaultPolicyNotEvaluated()
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: true, policy2Result: true, defaultPolicy1Result: false, defaultPolicy2Result: false);
            var service = new TestSecurityPolicyService(policyData);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            service.Mocks.MockPolicyHandler1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            service.Mocks.MockPolicyHandler2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
        }

        [Fact]
        public async Task EvaluateAsync_WhenDefaultSecurityPolicyNotMetReturnFailure()
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: true, policy2Result: true, defaultPolicy1Result: true, defaultPolicy2Result: false);
            var configuration = new AppConfiguration() { EnforceDefaultSecurityPolicies = true };
            var service = new TestSecurityPolicyService(policyData, null, null, null, null, configuration);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.Equal(false, result.Success);
            
            // The error indicates which subscription failed
            Assert.Contains(policyData.DefaultSubscription.Object.SubscriptionName, result.ErrorMessage);

            // Audit record is saved
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Once);

            // Policies are evaluated only once
            service.Mocks.MockPolicyHandler1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            service.Mocks.MockPolicyHandler2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EvaluateAsync_WhenDefaultSecurityPolicyIsMetUserPolicyIsEvaluated(bool userPolicyMet)
        {
            // Arrange
            var policyData = new TestUserSecurityPolicyData(policy1Result: true, policy2Result: userPolicyMet, defaultPolicy1Result: true, defaultPolicy2Result: true);
            var configuration = new AppConfiguration() { EnforceDefaultSecurityPolicies = true };
            var service = new TestSecurityPolicyService(policyData, null, null, null, null, configuration);
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act
            var result = await service.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, CreateHttpContext(user));

            // Assert
            Assert.Equal(userPolicyMet, result.Success);

            // Default policies and user policies are evaluated
            service.Mocks.MockPolicyHandler1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Exactly(2));
            service.Mocks.MockPolicyHandler2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void IsSubscribed_ThrowsArgumentIfUsernameMissing(string username)
        {
            Assert.Throws<ArgumentException>(() => new TestSecurityPolicyService().IsSubscribed(new User(), username));
        }

        [Fact]
        public void IsSubscribed_ThrowsArgumentNullIfUserIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestSecurityPolicyService().IsSubscribed(null, new Mock<IUserSecurityPolicySubscription>().Object));
        }

        [Fact]
        public void IsSubscribed_ThrowsArgumentNullIfSubscriptionIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestSecurityPolicyService().IsSubscribed(new User(), (IUserSecurityPolicySubscription)null));
        }

        [Fact]
        public void IsSubscribed_ReturnsTrueIfUserHasSubscriptionPolicies()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");
            var subscription = service.Mocks.Subscription.Object;
            user.SecurityPolicies = subscription.Policies.ToList();

            // Act & Assert.
            Assert.True(service.IsSubscribed(user, service.UserSubscriptions.Single()));
        }

        [Fact]
        public void IsSubscribed_ReturnsTrueIfUserHasSubscriptionAndOtherPolicies()
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
        public void IsSubscribed_ReturnsFalseIfUserDoesNotHaveAllSubscriptionPolicies()
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
        public void SubscribeAsync_ThrowsArgumentNullIfUserIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(null, new Mock<IUserSecurityPolicySubscription>().Object));
        }

        [Fact]
        public void SubscribeAsync_ThrowsArgumentNullIfSubscriptionIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(new User(), (IUserSecurityPolicySubscription)null));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void SubscribeAsync_ThrowsArgumentNullIfSubscriptionNameIsMissing(string subscriptionName)
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().SubscribeAsync(new User(), subscriptionName));
        }

        [Fact]
        public async Task SubscribeAsync_AddsAllSubscriptionPoliciesWhenHasNoneToStart()
        {
            // Arrange.
            var service = new TestSecurityPolicyService();
            var user = new User("testUser");

            // Act.
            var subscribed = await service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.True(subscribed);
            Assert.Equal(2, user.SecurityPolicies.Count);
            service.Mocks.VerifySubscriptionPolicies(user.SecurityPolicies);

            service.Mocks.Subscription.Verify(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SubscribeAsync_AddsAllSubscriptionPoliciesWhenHasSameAsDifferentSubscription()
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
            var subscribed = await service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.True(subscribed);

            var policies = user.SecurityPolicies.ToList();
            Assert.Equal(4, policies.Count);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            Assert.Equal(subscriptionName2, policies[0].Subscription);
            service.Mocks.VerifySubscriptionPolicies(policies.Skip(2));

            service.Mocks.Subscription.Verify(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Once);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SubscribeAsync_DoesNotAddSubscriptionPoliciesIfAlreadySubscribed()
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
            var subscribed = await service.SubscribeAsync(user, service.UserSubscriptions.First());

            // Act & Assert.
            Assert.False(subscribed);
            Assert.Equal(2, user.SecurityPolicies.Count);
            service.Mocks.VerifySubscriptionPolicies(user.SecurityPolicies);

            service.Mocks.Subscription.Verify(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()), Times.Never);
            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task SubscribeAsync_SavesAuditRecordIfWasNotSubscribed()
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
        public async Task SubscribeAsync_DoesNotSaveAuditRecordIfWasAlreadySubscribed()
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
        public void UnsubscribeAsync_ThrowsArgumentNullIfUserIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(null, new Mock<IUserSecurityPolicySubscription>().Object));
        }

        [Fact]
        public void UnsubscribeAsync_ThrowsArgumentNullIfSubscriptionIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(new User(), (IUserSecurityPolicySubscription)null));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void UnsubscribeAsync_ThrowsArgumentNullIfSubscriptionNameIsMissing(string subscriptionName)
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                new TestSecurityPolicyService().UnsubscribeAsync(new User(), subscriptionName));
        }

        [Fact]
        public async Task UnsubscribeAsync_RemovesAllSubscriptionPolicies()
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
        public async Task UnsubscribeAsync_DoesNotRemoveOtherSubscriptionPolicies()
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
        public async Task UnsubscribeAsync_RemovesNoPoliciesIfNotSubscribed()
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
        public async Task UnsubscribeAsync_SavesAuditRecordIfWasSubscribed()
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
        public async Task UnsubscribeAsync_DoesNotSaveAuditRecordIfWasNotSubscribed()
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
