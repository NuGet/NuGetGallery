// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Filters;
using Xunit;

namespace NuGetGallery.Security
{
    public class TestUserSecurityPolicyData
    {
        private const string MockSubscriptionName = "MockSubscription";

        public TestUserSecurityPolicyData(bool policy1Result = true, bool policy2Result = true)
        {
            MockPolicy1 = MockHandler(nameof(MockPolicy1), policy1Result);
            MockPolicy2 = MockHandler(nameof(MockPolicy2), policy2Result);

            Subscription = new Mock<IUserSecurityPolicySubscription>();
            Subscription.Setup(s => s.SubscriptionName).Returns(MockSubscriptionName);
            Subscription.Setup(s => s.OnSubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()))
                .Returns(Task.CompletedTask).Verifiable();
            Subscription.Setup(s => s.OnUnsubscribeAsync(It.IsAny<UserSecurityPolicySubscriptionContext>()))
                .Returns(Task.CompletedTask).Verifiable();
            Subscription.Setup(s => s.Policies).Returns(GetMockPolicies());
        }

        public Mock<IUserSecurityPolicySubscription> Subscription { get; }

        public Mock<UserSecurityPolicyHandler> MockPolicy1 { get; }

        public Mock<UserSecurityPolicyHandler> MockPolicy2 { get; }

        public IEnumerable<Mock<UserSecurityPolicyHandler>> Handlers
        {
            get
            {
                yield return MockPolicy1;
                yield return MockPolicy2;
            }
        }

        public void VerifySubscriptionPolicies(IEnumerable<UserSecurityPolicy> actualPolicies)
        {
            var actual = actualPolicies.ToList();
            var expected = Subscription.Object.Policies.ToList();

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
                failedPolicy = expectedPolicy1 ? nameof(MockPolicy2) : nameof(MockPolicy1);
            }
            Assert.Equal(failedPolicy, actual.ErrorMessage);
            
            MockPolicy1.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()), Times.Once);
            MockPolicy2.Verify(p => p.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>()),
                expectedPolicy2.HasValue ? Times.Once() : Times.Never());
        }

        private static IEnumerable<UserSecurityPolicy> GetMockPolicies()
        {
            yield return new UserSecurityPolicy(nameof(MockPolicy1), MockSubscriptionName);
            yield return new UserSecurityPolicy(nameof(MockPolicy2), MockSubscriptionName);
        }

        private Mock<UserSecurityPolicyHandler> MockHandler(string name, bool success)
        {
            var result = success ? SecurityPolicyResult.SuccessResult : SecurityPolicyResult.CreateErrorResult(name);
            var mock = new Mock<UserSecurityPolicyHandler>(name, SecurityPolicyAction.PackagePush);
            mock.Setup(m => m.Evaluate(It.IsAny<UserSecurityPolicyEvaluationContext>())).Returns(result).Verifiable();
            return mock;
        }
    }
}
