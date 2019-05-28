// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Services.Security;
using Xunit;

namespace NuGetGallery.Security
{
    public class AutomaticallyOverwriteRequiredSignerPolicyFacts
    {
        [Fact]
        public void PolicyName_IsTypeName()
        {
            Assert.Equal(
                nameof(AutomaticallyOverwriteRequiredSignerPolicy),
                AutomaticallyOverwriteRequiredSignerPolicy.PolicyName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var policy = new AutomaticallyOverwriteRequiredSignerPolicy();

            Assert.Equal(nameof(AutomaticallyOverwriteRequiredSignerPolicy), policy.Name);
            Assert.Equal(nameof(AutomaticallyOverwriteRequiredSignerPolicy), policy.SubscriptionName);
            Assert.Equal(SecurityPolicyAction.AutomaticallyOverwriteRequiredSigner, policy.Action);
            Assert.Single(policy.Policies);
            Assert.Equal(nameof(AutomaticallyOverwriteRequiredSignerPolicy), policy.Policies.Single().Name);
            Assert.Equal(nameof(AutomaticallyOverwriteRequiredSignerPolicy), policy.Policies.Single().Subscription);
        }

        [Fact]
        public void Evaluate_Throws()
        {
            var policy = new AutomaticallyOverwriteRequiredSignerPolicy();

            Assert.ThrowsAsync<NotImplementedException>(() => policy.EvaluateAsync(context: null));
        }

        [Fact]
        public void OnSubscribeAsync_ReturnsCompletedTask()
        {
            var policy = new AutomaticallyOverwriteRequiredSignerPolicy();

            var task = policy.OnSubscribeAsync(context: null);

            Assert.Same(Task.CompletedTask, task);
        }

        [Fact]
        public void OnUnsubscribeAsync_ReturnsCompletedTask()
        {
            var policy = new AutomaticallyOverwriteRequiredSignerPolicy();

            var task = policy.OnSubscribeAsync(context: null);

            Assert.Same(Task.CompletedTask, task);
        }
    }
}