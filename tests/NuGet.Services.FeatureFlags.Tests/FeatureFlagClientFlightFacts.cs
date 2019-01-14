// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.FeatureFlags.Tests
{
    public class FeatureFlagClientFlightFacts
    {
        /// <summary>
        /// Tests <see cref="FeatureFlagClient.IsEnabled(string, IFlightUser, bool)"/>
        /// </summary>
        public class IsEnabled_Flights : FactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenLatestFlagsReturnsNull_ReturnsDefault(bool defaultValue)
            {
                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns<FeatureFlags>(null);

                Assert.Equal(defaultValue, _target.IsEnabled("Flight", _user, defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenFlightUnknown_ReturnsDefault(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFlight("Flight", new Flight(all: false, siteAdmins: false, accounts: new List<string>(), domains: new List<string>()))
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.Equal(defaultValue, _target.IsEnabled("Unknown", _user, defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenFlightDisabled_ReturnsFalse(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFlight("Flight", new Flight(all: false, siteAdmins: false, accounts: new List<string>(), domains: new List<string>()))
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.False(_target.IsEnabled("Flight", _user, defaultValue));
                Assert.False(_target.IsEnabled("flight", _user, defaultValue));

                Assert.False(_target.IsEnabled("Flight", _admin, defaultValue));
                Assert.False(_target.IsEnabled("flight", _admin, defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenFlightEnabledForAll_ReturnsTrue(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFlight("Flight", new Flight(all: true, siteAdmins: false, accounts: new List<string>(), domains: new List<string>()))
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.True(_target.IsEnabled("Flight", _user, defaultValue));
                Assert.True(_target.IsEnabled("flight", _user, defaultValue));

                Assert.True(_target.IsEnabled("Flight", _admin, defaultValue));
                Assert.True(_target.IsEnabled("flight", _admin, defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenAccountIsFlighted_ReturnsTrue(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFlight("Flight", new Flight(all: false, siteAdmins: false, accounts: new List<string> { "Alice", "case_test" }, domains: new List<string>()))
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.False(_target.IsEnabled("Flight", _user, defaultValue));
                Assert.False(_target.IsEnabled("flight", _user, defaultValue));

                Assert.True(_target.IsEnabled("Flight", _admin, defaultValue));
                Assert.True(_target.IsEnabled("flight", _admin, defaultValue));

                // Account names should be case insensitive
                var user2 = new TestFlightUser { Username = "CASE_TEST", EmailAddress = "test@nuget.org" };

                Assert.True(_target.IsEnabled("Flight", user2, defaultValue));
                Assert.True(_target.IsEnabled("flight", user2, defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenAccountEmailAddressDomainFlighted_ReturnsTrue(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFlight("Flight", new Flight(all: false, siteAdmins: false, accounts: new List<string>(), domains: new List<string> { "nuget.org" }))
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.True(_target.IsEnabled("Flight", _admin, defaultValue));
                Assert.True(_target.IsEnabled("flight", _admin, defaultValue));

                Assert.False(_target.IsEnabled("Flight", _user, defaultValue));
                Assert.False(_target.IsEnabled("flight", _user, defaultValue));

                // Domains should be case insensitive
                var user2 = new TestFlightUser { Username = "case_test", EmailAddress = "TEST@NUGET.ORG" };

                Assert.True(_target.IsEnabled("Flight", user2, defaultValue));
                Assert.True(_target.IsEnabled("flight", user2, defaultValue));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenAdminsFlighted_ReturnsTrue(bool defaultValue)
            {
                var latestFlags = FeatureFlagStateBuilder
                    .Create()
                    .WithFlight("Flight", new Flight(all: false, siteAdmins: true, accounts: new List<string>(), domains: new List<string>()))
                    .Build();

                _cache
                    .Setup(f => f.GetLatestFlagsOrNull())
                    .Returns(latestFlags);

                Assert.False(_target.IsEnabled("Flight", _user, defaultValue));
                Assert.False(_target.IsEnabled("flight", _user, defaultValue));

                Assert.True(_target.IsEnabled("flight", _admin, defaultValue));
                Assert.True(_target.IsEnabled("Flight", _admin, defaultValue));
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IFeatureFlagCacheService> _cache;
            protected readonly FeatureFlagClient _target;

            protected readonly IFlightUser _user;
            protected readonly IFlightUser _admin;

            public FactsBase()
            {
                _cache = new Mock<IFeatureFlagCacheService>();

                _target = new FeatureFlagClient(
                    _cache.Object,
                    Mock.Of<ILogger<FeatureFlagClient>>());

                _user = new TestFlightUser
                {
                    Username = "Bob",
                    EmailAddress = "hello@bob.org",
                    IsSiteAdmin = false,
                };

                _admin = new TestFlightUser
                {
                    Username = "Alice",
                    EmailAddress = "admin@nuget.org",
                    IsSiteAdmin = true,
                };
            }

            protected class TestFlightUser : IFlightUser
            {
                public string Username { get; set; }
                public string EmailAddress { get; set; }
                public bool IsSiteAdmin { get; set; }
            }
        }
    }
}
