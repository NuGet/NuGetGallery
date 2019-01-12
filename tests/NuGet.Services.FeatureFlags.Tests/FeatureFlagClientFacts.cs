// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.FeatureFlags.Tests
{
    public class FeatureFlagClientFacts
    {
        /// <summary>
        /// Tests <see cref="FeatureFlagClient.IsEnabled(string, bool)"/>
        /// </summary>
        public class IsEnabled_FeatureFlags : FactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenLatestFlagsReturnsNull_ReturnsDefaultValue(bool defaultValue)
            {
                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns<FeatureFlags>(null);

                Assert.Equal(defaultValue, _target.IsEnabled("Feature", defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenFlagUnknown_ReturnsDefault(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder.Create().Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.Equal(defaultValue, _target.IsEnabled("Feature", defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenFlagEnabled_ReturnsTrue(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFeature("Feature", FeatureStatus.Enabled)
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.True(_target.IsEnabled("Feature", defaultValue));
                Assert.True(_target.IsEnabled("feature", defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenFlagDisabled_ReturnsFalse(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFeature("Feature", FeatureStatus.Disabled)
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.False(_target.IsEnabled("Feature", defaultValue));
                Assert.False(_target.IsEnabled("feature", defaultValue));
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IFeatureFlagCacheService> _cache;
            protected readonly FeatureFlagClient _target;

            public FactsBase()
            {
                _cache = new Mock<IFeatureFlagCacheService>();

                _target = new FeatureFlagClient(
                    _cache.Object,
                    Mock.Of<ILogger<FeatureFlagClient>>());
            }
        }
    }
}
