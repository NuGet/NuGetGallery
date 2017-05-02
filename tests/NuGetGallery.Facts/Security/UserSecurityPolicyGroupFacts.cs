// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Packaging;
using Xunit;
using System.Web.Mvc;

namespace NuGetGallery.Security
{
    public class UserSecurityPolicyGroupFacts
    {
        [Theory]
        [InlineData("")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
        public void SecurePush_OnEnrollExpiresPushApiKeys(string scopes)
        {
            // Arrange.
            var group = UserSecurityPolicyGroup.Instances.First(
                g => g.Name.Equals(UserSecurityPolicyGroup.SecurePush, StringComparison.OrdinalIgnoreCase));

            var credential = new Credential(CredentialTypes.ApiKey.V2, string.Empty, TimeSpan.FromDays(10));
            if (!string.IsNullOrWhiteSpace(scopes))
            {
                credential.Scopes.AddRange(JsonConvert.DeserializeObject<List<Scope>>(scopes));
            }
            var user = new User();
            user.Credentials.Add(credential);

            // Act.
            user.AddPolicies(group);

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
            // Arrange.
            var group = UserSecurityPolicyGroup.Instances.First(
                g => g.Name.Equals(UserSecurityPolicyGroup.SecurePush, StringComparison.OrdinalIgnoreCase));

            var credential = new Credential(type, string.Empty, TimeSpan.FromDays(10));
            if (!string.IsNullOrWhiteSpace(scopes))
            {
                credential.Scopes.AddRange(JsonConvert.DeserializeObject<List<Scope>>(scopes));
            }
            var user = new User();
            user.Credentials.Add(credential);

            // Act.
            user.AddPolicies(group);

            // Assert.
            Assert.Equal(2, user.SecurityPolicies.Count());
            Assert.False(DateTime.UtcNow.AddDays(7) >= user.Credentials.First().Expires);
        }
    }
}
