// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    public class TestSecurityPolicyService : SecurityPolicyService
    {
        public const string MockSubscriptionName = "MockSubscription";

        public Mock<IEntitiesContext> MockEntitiesContext { get; }

        public Mock<IDbSet<UserSecurityPolicy>> MockUserSecurityPolicies { get; }

        public Mock<IUserSecurityPolicySubscription> MockSubscription { get; }

        public Mock<UserSecurityPolicyHandler> MockPolicy1 { get; }

        public Mock<UserSecurityPolicyHandler> MockPolicy2 { get; }

        public TestSecurityPolicyService(Mock<IEntitiesContext> mockEntitiesContext = null,
            bool success1 = true, bool success2 = true)
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
            MockSubscription.Setup(s => s.SubscriptionName).Returns(MockSubscriptionName);
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
