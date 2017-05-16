﻿// Copyright (c) .NET Foundation. All rights reserved.
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
            var policy1 = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequireMinClientVersionForPushPolicy.PolicyName));
            var policy2 = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequirePackageVerifyScopePolicy.PolicyName));

            // Act & Assert.
            Assert.Equal(2, subscription.Policies.Count());
            Assert.NotNull(policy1);
            Assert.NotNull(policy2);
            Assert.Equal("{\"v\":\"4.1.0\"}", policy1.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
        public async Task OnSubscribeAsync_ExpiresPushApiKeys(string scopes)
        {
            // Arrange & Act.
            var user = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2, scopes)).Item1;

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(_expectedPushKeyExpiration) >= user.Credentials.Single().Expires);
        }

        [Theory]
        [InlineData("password.v3", "")]
        [InlineData("apikey.v2", "[{\"a\":\"package:unlist\", \"s\":\"theId\"}]")]
        [InlineData("apikey.verify.v1", "[{\"a\":\"package:verify\", \"s\":\"theId\"}]")]
        public async Task OnSubscribeAsync_DoesNotExpireNonPushCredentials(string type, string scopes)
        {
            // Arrange & Act.
            var user = (await SubscribeUserToSecurePushAsync(type, scopes)).Item1;

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.False(DateTime.UtcNow.AddDays(_expectedPushKeyExpiration) >= user.Credentials.Single().Expires);
        }

        [Theory]
        [InlineData("")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
        public async Task OnSubscribeAsync_DoesNotChangeExpiringPushCredentials(string scopes)
        {
            // Arrange & Act.
            var user = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2,scopes, expiresInDays: 2)).Item1;

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(2) >= user.Credentials.Single().Expires);
        }

        [Fact]
        public async Task OnSubscribeAsync_SavesAuditRecordIfKeysSetToExpire()
        {
            // Arrange & Act.
            var service = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2, "")).Item2;

            // Assert.
            service.MockAuditingService.Verify(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()),
                /* subscription and key expiration */Times.Exactly(2));
        }

        [Fact]
        public async Task OnSubscribeAsync_DoesNotSaveAuditRecordIfKeysNotSetToExpire()
        {
            // Arrange & Act.
            var service = (await SubscribeUserToSecurePushAsync(CredentialTypes.ApiKey.V2, "", expiresInDays: 2)).Item2;

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
            string type, string scopes, int expiresInDays = 100)
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
            await service.SubscribeAsync(user, service.UserSubscriptions.Single());

            service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);

            return Tuple.Create(user, service);
        }
    }
}
