// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Security
{
    public class DefaultSubscriptionFacts
    {
        [Fact]
        public void Policies_ReturnsMinClientAndPackageVerifyScopePolicies()
        {
            // Arrange.
            var subscription = CreateSecurityPolicyService().Subscriptions.Single();
            var policy1 = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequireMinProtocolVersionForPushPolicy.PolicyName));
            var policy2 = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequirePackageVerifyScopePolicy.PolicyName));

            // Act & Assert.
            Assert.Equal(2, subscription.Policies.Count());
            Assert.NotNull(policy1);
            Assert.NotNull(policy2);
            Assert.Equal("{\"v\":\"4.1.0\"}", policy1.Value);
        }

        private TestSecurityPolicyService CreateSecurityPolicyService()
        {
            var auditing = new Mock<IAuditingService>();
            auditing.Setup(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>())).Returns(Task.CompletedTask).Verifiable();

            var subscription = new DefaultSubscription();

            var service = new TestSecurityPolicyService(
                mockAuditing: auditing,
                userHandlers: new UserSecurityPolicyHandler[]
                {
                    new RequireMinProtocolVersionForPushPolicy(),
                    new RequirePackageVerifyScopePolicy()
                },
                userSubscriptions: new[] { subscription });

            return service;
        }
    }
}
