// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    /// <summary>
    /// Verifies that promotion-worker configuration can override its concurrency default.
    /// </summary>
    public class JobFacts : Job
    {
        [Theory]
        [InlineData(null, 1)]
        [InlineData("4", 4)]
        public void UsesConfiguredConcurrencyOrDefaultsToOne(string configuredValue, int expected)
        {
            var settings = new Dictionary<string, string>();
            if (configuredValue != null)
            {
                settings.Add("ServiceBus:MaxConcurrentCalls", configuredValue);
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var services = new ServiceCollection();
            ConfigureJobServices(services, configuration);
            using (var provider = services.BuildServiceProvider())
            {
                var options = provider.GetRequiredService<IOptionsSnapshot<SubscriptionProcessorConfiguration>>().Value;

                Assert.Equal(expected, options.MaxConcurrentCalls);
            }
        }
    }
}
