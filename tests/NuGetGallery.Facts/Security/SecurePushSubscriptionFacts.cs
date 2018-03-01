// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGetGallery.Auditing;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Security
{
    public class SecurePushSubscriptionFacts
    {
        private int _expectedPushKeyExpiration = 30;

        [Fact]
        public void SecurePushSubscription_IsRegisteredWithSecurityPolicyService()
        {
            // Arrange.
            var subscriptions = CreateSecurityPolicyService().UserSubscriptions;

            // Act & Assert.
            Assert.Equal(1, subscriptions.Count());
            Assert.Equal("SecurePush", subscriptions.Single().SubscriptionName);
        }

        [Fact]
        public void Policies_ReturnsMinClientAndPackageVerifyScopePolicies()
        {
            // Arrange.
            var subscription = CreateSecurityPolicyService().UserSubscriptions.Single();
            var policy1 = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequireMinProtocolVersionForPushPolicy.PolicyName));
            var policy2 = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequirePackageVerifyScopePolicy.PolicyName));

            // Act & Assert.
            Assert.Equal(2, subscription.Policies.Count());
            Assert.NotNull(policy1);
            Assert.NotNull(policy2);
            Assert.Equal("{\"v\":\"4.1.0\"}", policy1.Value);
        }

        public static IEnumerable<object[]> OnSubscribeAsync_ExpiresPushApiKeys_Data
        {
            get
            {
                foreach (var scopes in new[] { "", "[{\"a\":\"package:push\", \"s\":\"theId\"}]", "[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]" })
                {
                    foreach (var commitChanges in new[] { false, true })
                    {
                        yield return MemberDataHelper.AsData(scopes, commitChanges);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(OnSubscribeAsync_ExpiresPushApiKeys_Data))]
        public async Task OnSubscribeAsync_ExpiresPushApiKeys(string scopes, bool commitChanges)
        {
            // Arrange & Act.
            var user = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2, scopes, commitChanges)).Item1;

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(_expectedPushKeyExpiration) >= user.Credentials.Single().Expires);
        }

        public static IEnumerable<object[]> OnSubscribeAsync_DoesNotExpireNonPushCredentials_Data
        {
            get
            {
                foreach (var commitChanges in new[] { false, true })
                {
                    yield return MemberDataHelper.AsData(CredentialTypes.Password.V3, "", commitChanges);
                    yield return MemberDataHelper.AsData(CredentialTypes.ApiKey.V2, "[{\"a\":\"package:unlist\", \"s\":\"theId\"}]", commitChanges);
                    yield return MemberDataHelper.AsData(CredentialTypes.ApiKey.VerifyV1, "[{\"a\":\"package:verify\", \"s\":\"theId\"}]", commitChanges);
                }
            }
        }

        [Theory]
        [MemberData(nameof(OnSubscribeAsync_DoesNotExpireNonPushCredentials_Data))]
        public async Task OnSubscribeAsync_DoesNotExpireNonPushCredentials(string type, string scopes, bool commitChanges)
        {
            // Arrange & Act.
            var user = (await SubscribeUserToSecurePushAsync(type, scopes, commitChanges)).Item1;

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.False(DateTime.UtcNow.AddDays(_expectedPushKeyExpiration) >= user.Credentials.Single().Expires);
        }

        public static IEnumerable<object[]> OnSubscribeAsync_DoesNotChangeExpiringPushCredentials_Data
        {
            get
            {
                foreach (var scopes in new[] { "", "[{\"a\":\"package:push\", \"s\":\"theId\"}]", "[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]" })
                {
                    foreach (var commitChanges in new[] { false, true })
                    {
                        yield return MemberDataHelper.AsData(scopes, commitChanges);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(OnSubscribeAsync_DoesNotChangeExpiringPushCredentials_Data))]
        public async Task OnSubscribeAsync_DoesNotChangeExpiringPushCredentials(string scopes, bool commitChanges)
        {
            // Arrange & Act.
            var user = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2,scopes, commitChanges, expiresInDays: 2)).Item1;

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(2) >= user.Credentials.Single().Expires);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OnSubscribeAsync_SavesAuditRecordIfKeysSetToExpire(bool commitChanges)
        {
            // Arrange & Act.
            var service = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2, "", commitChanges)).Item2;

            // Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()),
                /* subscription and key expiration */Times.Exactly(2));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OnSubscribeAsync_DoesNotSaveAuditRecordIfKeysNotSetToExpire(bool commitChanges)
        {
            // Arrange & Act.
            var service = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2, "", commitChanges, expiresInDays: 2)).Item2;

            // Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()),
                /* subscription only */ Times.Once);
        }

        private TestSecurityPolicyService CreateSecurityPolicyService()
        {
            var auditing = new Mock<IAuditingService>();
            auditing.Setup(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>())).Returns(Task.CompletedTask).Verifiable();

            var diagnostics = new DiagnosticsService().GetSource(nameof(SecurePushSubscriptionFacts));
            var diagnosticsService = new Mock<IDiagnosticsService>();
            diagnosticsService.Setup(s => s.GetSource(It.IsAny<string>())).Returns(diagnostics);

            var subscription = new SecurePushSubscription(auditing.Object, diagnosticsService.Object);

            var service = new TestSecurityPolicyService(
                mockAuditing: auditing,
                userHandlers: new UserSecurityPolicyHandler[]
                {
                    new RequireMinClientVersionForPushPolicy(),
                    new RequirePackageVerifyScopePolicy()
                },
                userSubscriptions: new[] { subscription });

            return service;
        }

        private async Task<Tuple<User, TestSecurityPolicyService>> SubscribeUserToSecurePushAsync(
            string type, string scopes, bool commitChanges, int expiresInDays = 100)
        {
            // Arrange.
            var service = CreateSecurityPolicyService();
            
            var credential = new Credential(type, string.Empty, TimeSpan.FromDays(expiresInDays));
            if (!string.IsNullOrWhiteSpace(scopes))
            {
                credential.Scopes.AddRange(JsonConvert.DeserializeObject<List<Scope>>(scopes));
            }
            var user = new User();
            user.Credentials.Add(credential);

            // Act.
            await service.SubscribeAsync(user, service.UserSubscriptions.Single(), commitChanges);

            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), commitChanges ? Times.Once() : Times.Never());

            return Tuple.Create(user, service);
        }
    }
}
