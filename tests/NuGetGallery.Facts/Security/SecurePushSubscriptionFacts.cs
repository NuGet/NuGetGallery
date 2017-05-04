// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using NuGet.Packaging;
using Xunit;

namespace NuGetGallery.Security
{
    public class SecurePushSubscriptionFacts
    {
        [Fact]
        public void SubscriptionName()
        {
            // Act & Assert.
            Assert.Equal("SecurePush", new SecurePushSubscription().Name);
        }

        [Fact]
        public void SubscriptionPolicies()
        {
            // Arrange.
            var subscription = new SecurePushSubscription();
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
        public void OnSubscribeExpiresPushApiKeysIn1Week(string scopes)
        {
            // Arrange & Act.
            var user = SubscribeUserToSecurePush(CredentialTypes.ApiKey.V2, scopes);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(7) >= user.Credentials.First().Expires);
        }

        [Theory]
        [InlineData("password.v3", "")]
        [InlineData("apikey.v2", "[{\"a\":\"package:unlist\", \"s\":\"theId\"}]")]
        [InlineData("apikey.verify.v1", "[{\"a\":\"package:verify\", \"s\":\"theId\"}]")]
        public void OnSubscribeDoesNotExpireNonPushCredentials(string type, string scopes)
        {
            // Arrange & Act.
            var user = SubscribeUserToSecurePush(type, scopes);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.False(DateTime.UtcNow.AddDays(7) >= user.Credentials.First().Expires);
        }

        [Theory]
        [InlineData("")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
        public void OnSubscribeDoesNotChangeExpiringPushCredentials(string scopes)
        {
            // Arrange & Act.
            var user = SubscribeUserToSecurePush(CredentialTypes.ApiKey.V2, scopes, expiresInDays: 2);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(2) >= user.Credentials.First().Expires);
        }

        private User SubscribeUserToSecurePush(string type, string scopes, int expiresInDays = 10)
        {
            // Arrange.
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext.Setup(c => c.SaveChangesAsync()).Returns(Task.FromResult(2)).Verifiable();

            var service = new SecurityPolicyService(entitiesContext.Object);
            var subscription = service.UserSubscriptions.First(s => s.Name.Equals(SecurePushSubscription.SubscriptionName));

            var credential = new Credential(type, string.Empty, TimeSpan.FromDays(expiresInDays));
            if (!string.IsNullOrWhiteSpace(scopes))
            {
                credential.Scopes.AddRange(JsonConvert.DeserializeObject<List<Scope>>(scopes));
            }
            var user = new User();
            user.Credentials.Add(credential);

            // Act.
            service.SubscribeAsync(user, subscription);
            entitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);

            return user;
        }
    }
}
