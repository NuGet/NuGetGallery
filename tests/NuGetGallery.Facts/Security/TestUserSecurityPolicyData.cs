// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Security;
using Xunit;

namespace NuGetGallery.Security
{
    public class TestUserSecurityPolicyData
    {
        private const string UserPoliciesSubscriptionName = "MockSubscription";
        private const string OrganizationPoliciesSubscriptionName = "MockSubscription2";
        private const string MockDefaultSubscriptionName = "Default";

        public TestUserSecurityPolicyData(bool policy1Result = true, bool policy2Result = true, bool defaultPolicy1Result = true, bool defaultPolicy2Result = true)
        {
            var resultPerSubscription1 = new Dictionary<string, bool>()
            {
                { UserPoliciesSubscriptionName, policy1Result },
                { MockDefaultSubscriptionName, defaultPolicy1Result }
            };

            var resultPerSubscription2 = new Dictionary<string, bool>()
            {
                { UserPoliciesSubscriptionName, policy2Result },
                { MockDefaultSubscriptionName, defaultPolicy2Result }
            };

            MockPolicyHandler1 = MockHandler(nameof(MockPolicyHandler1), resultPerSubscription1);
            MockPolicyHandler2 = MockHandler(nameof(MockPolicyHandler2), resultPerSubscription2);

            UserPoliciesSubscription = CreateSubscriptionMock(UserPoliciesSubscriptionName);
            OrganizationPoliciesSubscription = CreateSubscriptionMock(OrganizationPoliciesSubscriptionName);

            DefaultSubscription = new Mock<IUserSecurityPolicySubscription>();
            DefaultSubscription.Setup(s => s.SubscriptionName).Returns(MockDefaultSubscriptionName);
            DefaultSubscription.Setup(s => s.Policies).Returns(GetMockPolicies(MockDefaultSubscriptionName));
        }

        private Mock<IUserSecurityPolicySubscription> CreateSubscriptionMock(string name)
        {
            var subscription = new Mock<IUserSecurityPolicySubscription>();

            subscription.Setup(s => s.SubscriptionName).Returns(name);
            subscription.Setup(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()))
                .Returns(Task.CompletedTask).Verifiable();
            subscription.Setup(s => s.OnUnsubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()))
                .Returns(Task.CompletedTask).Verifiable();
            subscription.Setup(s => s.Policies).Returns(GetMockPolicies(name));

            return subscription;
        }

        public Mock<IUserSecurityPolicySubscription> UserPoliciesSubscription { get; }

        public Mock<IUserSecurityPolicySubscription> OrganizationPoliciesSubscription { get; }

        public Mock<IUserSecurityPolicySubscription> DefaultSubscription { get; }

        public Mock<UserSecurityPolicyHandler> MockPolicyHandler1 { get; }

        public Mock<UserSecurityPolicyHandler> MockPolicyHandler2 { get; }

        public IEnumerable<Mock<UserSecurityPolicyHandler>> Handlers
        {
            get
            {
                yield return MockPolicyHandler1;
                yield return MockPolicyHandler2;
            }
        }

        public void VerifySubscriptionPolicies(IEnumerable<UserSecurityPolicy> actualPolicies)
        {
            var actual = actualPolicies.ToList();
            var expected = UserPoliciesSubscription.Object.Policies.ToList();

            Assert.Equal(expected.Count, actual.Count);
            
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i].Equals(actual[i]));
            }
        }

        public void VerifyPolicyEvaluation(bool expectedPolicy1, bool? expectedPolicy2, SecurityPolicyResult actual)
        {
            var expectedSuccess = expectedPolicy1 && expectedPolicy2.Value;
            Assert.Equal(expectedSuccess, actual.Success);

            string failedPolicy = null;
            if (!expectedSuccess)
            {
                failedPolicy = expectedPolicy1 ? nameof(MockPolicyHandler2) : nameof(MockPolicyHandler1);
            }
            Assert.Contains(failedPolicy, actual.ErrorMessage);
            
            MockPolicyHandler1.Verify(p => p.EvaluateAsync(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            MockPolicyHandler2.Verify(p => p.EvaluateAsync(It.IsAny<UserSecurityPolicyEvaluationContext>()),
                expectedPolicy2.HasValue ? Times.Once() : Times.Never());
        }

        private static IEnumerable<UserSecurityPolicy> GetMockPolicies(string subscriptionName)
        {
            yield return new UserSecurityPolicy(nameof(MockPolicyHandler1), subscriptionName);
            yield return new UserSecurityPolicy(nameof(MockPolicyHandler2), subscriptionName);
        }

        private static Mock<UserSecurityPolicyHandler> MockHandler(string name, Dictionary<string, bool> resultPerSubscription)
        {
            var mock = new Mock<UserSecurityPolicyHandler>(name, SecurityPolicyAction.PackagePush);
            mock.Setup(m => m.EvaluateAsync(It.IsAny<UserSecurityPolicyEvaluationContext>()))
                .Returns<UserSecurityPolicyEvaluationContext>(x =>
                {
                    var subscription = x.Policies.First().Subscription;
                    return resultPerSubscription[subscription] == true ? 
                        Task.FromResult(SecurityPolicyResult.SuccessResult) :
                        Task.FromResult(SecurityPolicyResult.CreateErrorResult($"{subscription}-{name}"));
                }).Verifiable();
            return mock;
        }
    }
}
