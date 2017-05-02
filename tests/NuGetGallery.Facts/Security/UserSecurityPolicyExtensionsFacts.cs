// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Newtonsoft.Json;
using System.Web.Mvc;

namespace NuGetGallery.Security
{
    public class UserSecurityPolicyExtensionsFacts
    {
        [Theory]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData(null, null)]
        public void MatchesReturnsTrue(string value1, string value2)
        {
            // Arrange.
            var policy1 = new UserSecurityPolicy("A") { Value = value1 };
            var policy2 = new UserSecurityPolicy("A") { Value = value2 };

            // Act & Assert.
            Assert.True(policy1.Matches(policy2));
        }

        [Fact]
        public void MatchesReturnsFalseIfNameDiffers()
        {
            // Arrange.
            var policy1 = new UserSecurityPolicy("A");
            var policy2 = new UserSecurityPolicy("B");

            // Act & Assert.
            Assert.False(policy1.Matches(policy2));
        }

        [Fact]
        public void MatchesReturnsFalseIfValueDiffers()
        {
            // Arrange.
            var policy1 = new UserSecurityPolicy("A") { Value = "B" };
            var policy2 = new UserSecurityPolicy("A") { Value = "C" };

            // Act & Assert.
            Assert.False(policy1.Matches(policy2));
        }

        [Theory]
        [InlineData("[[\"A\",\"\"]]", "[[\"A\",null]]")]
        [InlineData("[[\"A\",\"B\"],[\"E\",\"\"]]", "[[\"E\",\"\"],[\"A\",\"B\"],[\"C\",\"D\"]]")]
        public void IsEnrolledReturnsTrue(string groupPolicies, string userPolicies)
        {
            // Arrange.
            var group = new UserSecurityPolicyGroup()
            {
                Policies = LoadPolicies(groupPolicies)
            };
            var user = new User();
            LoadPolicies(userPolicies).ToList().ForEach(p => user.SecurityPolicies.Add(p));

            // Act & Assert.
            Assert.True(user.IsEnrolled(group));
        }

        [Theory]
        [InlineData("[[\"A\",\"B\"],[\"E\",null]]", "[]")]
        [InlineData("[[\"A\",\"B\"],[\"E\",null]]", "[[\"A\",\"B\"],[\"C\",\"D\"]]")]
        public void IsEnrolledReturnsFalse(string groupPolicies, string userPolicies)
        {
            // Arrange.
            var group = new UserSecurityPolicyGroup()
            {
                Policies = LoadPolicies(groupPolicies)
            };
            var user = new User();
            LoadPolicies(userPolicies).ToList().ForEach(p => user.SecurityPolicies.Add(p));

            // Act & Assert.
            Assert.False(user.IsEnrolled(group));
        }

        [Theory]
        [InlineData("[[\"A\",\"\"]]")]
        [InlineData("[[\"A\",\"B\"],[\"E\",\"\"]]")]
        public void AddPoliciesAddsPolicies(string groupPolicies)
        {
            // Arrange.
            var group = new UserSecurityPolicyGroup()
            {
                Policies = LoadPolicies(groupPolicies)
            };
            var user = new User();

            // Act.
            user.AddPolicies(group);

            // Assert.
            Assert.Equal(group.Policies.Count(), user.SecurityPolicies.Count());
        }

        [Theory]
        [InlineData("[[\"A\",\"\"]]", "[[\"A\",null]]", 0)]
        [InlineData("[[\"A\",\"B\"],[\"E\",\"\"]]", "[[\"E\",\"\"],[\"A\",\"B\"],[\"C\",\"D\"]]", 1)]
        public void RemovePoliciesRemovesPolicies(string groupPolicies, string userPolicies, int expectedCount)
        {
            // Arrange.
            var group = new UserSecurityPolicyGroup()
            {
                Policies = LoadPolicies(groupPolicies)
            };
            var user = new User();
            LoadPolicies(userPolicies).ToList().ForEach(p => user.SecurityPolicies.Add(p));

            // Act.
            user.RemovePolicies(group);

            // Assert.
            Assert.Equal(expectedCount, user.SecurityPolicies.Count());
        }

        private IEnumerable<UserSecurityPolicy> LoadPolicies(string policiesString)
        {
            var policies = (string[][])JsonConvert.DeserializeObject<string[][]>(policiesString);
            if (policies != null)
            {
                foreach (var p in policies)
                {
                    yield return new UserSecurityPolicy(p[0]) { Value = p[1] };
                }
            }
        }
    }
}
