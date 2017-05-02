// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Packaging;
using Xunit;

namespace NuGetGallery.Security
{
    public class UserSecurityPolicyGroupFacts
    {
        [Theory]
        [InlineData("")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
        public void SecurePush_OnEnrollExpiresPushApiKeys(string scopes)
        {
            // Arrange & Act.
            var user = EnrollUserInSecurePush(CredentialTypes.ApiKey.V2, scopes);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(7) >= user.Credentials.First().Expires);
        }

        [Theory]
        [InlineData("password.v3", "")]
        [InlineData("apikey.v2", "[{\"a\":\"package:unlist\", \"s\":\"theId\"}]")]
        [InlineData("apikey.verify.v1", "[{\"a\":\"package:verify\", \"s\":\"theId\"}]")]
        public void SecurePush_OnEnrollDoesNotExpireNonPushCredentials(string type, string scopes)
        {
            // Arrange & Act.
            var user = EnrollUserInSecurePush(type, scopes);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.False(DateTime.UtcNow.AddDays(7) >= user.Credentials.First().Expires);
        }

        [Theory]
        [InlineData("")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
        public void SecurePush_OnEnrollDoesNotChangeExpiringPushCredentials(string scopes)
        {
            // Arrange & Act.
            var user = EnrollUserInSecurePush(CredentialTypes.ApiKey.V2, scopes, expiresInDays: 2);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.True(DateTime.UtcNow.AddDays(2) >= user.Credentials.First().Expires);
        }

        private User EnrollUserInSecurePush(string type, string scopes, int expiresInDays = 10)
        {
            // Arrange.
            var group = UserSecurityPolicyGroup.Instances.First(
                g => g.Name.Equals(UserSecurityPolicyGroup.SecurePush, StringComparison.OrdinalIgnoreCase));

            var credential = new Credential(type, string.Empty, TimeSpan.FromDays(expiresInDays));
            if (!string.IsNullOrWhiteSpace(scopes))
            {
                credential.Scopes.AddRange(JsonConvert.DeserializeObject<List<Scope>>(scopes));
            }
            var user = new User();
            user.Credentials.Add(credential);

            // Act.
            user.AddPolicies(group);

            return user;
        }
    }
}
