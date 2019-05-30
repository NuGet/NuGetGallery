// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Security
{
    public class ControlRequiredSignerPolicyFacts
    {
        [Fact]
        public void PolicyName_IsTypeName()
        {
            Assert.Equal(nameof(ControlRequiredSignerPolicy), ControlRequiredSignerPolicy.PolicyName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var policy = new ControlRequiredSignerPolicy();

            Assert.Equal(nameof(ControlRequiredSignerPolicy), policy.Name);
            Assert.Equal(nameof(ControlRequiredSignerPolicy), policy.SubscriptionName);
            Assert.Equal(SecurityPolicyAction.ControlRequiredSigner, policy.Action);
            Assert.Single(policy.Policies);
            Assert.Equal(nameof(ControlRequiredSignerPolicy), policy.Policies.Single().Name);
            Assert.Equal(nameof(ControlRequiredSignerPolicy), policy.Policies.Single().Subscription);
        }

        [Fact]
        public void Evaluate_Throws()
        {
            var policy = new ControlRequiredSignerPolicy();

            Assert.ThrowsAsync<NotImplementedException>(() => policy.EvaluateAsync(context: null));
        }

        [Fact]
        public void OnSubscribeAsync_ReturnsCompletedTask()
        {
            var policy = new ControlRequiredSignerPolicy();

            var task = policy.OnSubscribeAsync(context: null);

            Assert.Same(Task.CompletedTask, task);
        }

        [Fact]
        public void OnUnsubscribeAsync_ReturnsCompletedTask()
        {
            var policy = new ControlRequiredSignerPolicy();

            var task = policy.OnSubscribeAsync(context: null);

            Assert.Same(Task.CompletedTask, task);
        }
    }
}