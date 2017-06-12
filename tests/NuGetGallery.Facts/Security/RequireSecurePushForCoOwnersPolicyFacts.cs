// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Security
{
    public class RequireSecurePushForCoOwnersPolicyFacts : TestContainer
    {
        private RequireSecurePushForCoOwnersPolicy _policy;

        public RequireSecurePushForCoOwnersPolicyFacts()
        {
            _policy = new RequireSecurePushForCoOwnersPolicy();
        }

        [Fact]
        public void PolicyHandlerName_ReturnsExpected()
        {
            Assert.Equal("RequireSecurePushForCoOwnersPolicy", _policy.Name);
        }

        [Fact]
        public void SubscriptionName_ReturnsExpected()
        {
            Assert.Equal("SecurePushForCoOwners", _policy.SubscriptionName);
        }

        [Fact]
        public void PolicyHandlerPolicies_ReturnsExpected()
        {
            Assert.Equal(1, _policy.Policies.Count());

            var policyModel = _policy.Policies.FirstOrDefault();
            Assert.NotNull(policyModel);

            Assert.Equal("RequireSecurePushForCoOwnersPolicy", policyModel.Name);
            Assert.Equal("SecurePushForCoOwners", policyModel.Subscription);
        }

        [Fact]
        public void IsSubscribed_ReturnsFalseIfNotSubscribed()
        {
            var fakes = Get<Fakes>();

            Assert.False(RequireSecurePushForCoOwnersPolicy.IsSubscribed(fakes.User));
        }

        [Fact]
        public void IsSubscribed_ReturnsTrueIfSubscribed()
        {
            var fakes = Get<Fakes>();
            fakes.User.SecurityPolicies = _policy.Policies.ToList();

            Assert.True(RequireSecurePushForCoOwnersPolicy.IsSubscribed(fakes.User));
        }
    }
}
