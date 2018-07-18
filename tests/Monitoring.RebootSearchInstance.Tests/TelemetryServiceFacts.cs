// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Logging;
using Xunit;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class TelemetryServiceFacts
    {
        private readonly Mock<ITelemetryClient> _telemetryClient;
        private readonly Mock<IOptionsSnapshot<MonitorConfiguration>> _configurationMock;
        private readonly MonitorConfiguration _configuration;
        private const string _subscription = "TEST";
        private const string _region = "USSC";
        private const int _count = 42;
        private const int _index = 1;
        private static readonly TimeSpan _duration = TimeSpan.FromMilliseconds(2342);
        private const InstanceHealth _health = InstanceHealth.Unknown;
        private readonly TelemetryService _target;
        private IDictionary<string, string> _properties;

        public TelemetryServiceFacts()
        {
            _telemetryClient = new Mock<ITelemetryClient>();
            _configurationMock = new Mock<IOptionsSnapshot<MonitorConfiguration>>();
            _configuration = new MonitorConfiguration
            {
                Subscription = _subscription,
            };

            _telemetryClient
                .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                .Callback<string, double, IDictionary<string, string>>((_, __, p) => _properties = p);
            _configurationMock
                .Setup(x => x.Value)
                .Returns(() => _configuration);

            _target = new TelemetryService(
                _telemetryClient.Object,
                _configurationMock.Object);
        }

        [Fact]
        public void TrackHealthyInstanceCount()
        {
            _target.TrackHealthyInstanceCount(_region, _count);

            _telemetryClient.Verify(
                x => x.TrackMetric(
                    "RebootSearchInstance.HealthyInstances",
                    _count,
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);
            Assert.NotNull(_properties);
            Assert.Equal(new[] { "Region", "Subscription" }, _properties.Keys.OrderBy(x => x));
            Assert.Equal(_region, _properties["Region"]);
            Assert.Equal(_subscription, _properties["Subscription"]);
        }

        [Fact]
        public void TrackUnhealthyInstanceCount()
        {
            _target.TrackUnhealthyInstanceCount(_region, _count);

            _telemetryClient.Verify(
                x => x.TrackMetric(
                    "RebootSearchInstance.UnhealthyInstances",
                    _count,
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);
            Assert.NotNull(_properties);
            Assert.Equal(new[] { "Region", "Subscription" }, _properties.Keys.OrderBy(x => x));
            Assert.Equal(_region, _properties["Region"]);
            Assert.Equal(_subscription, _properties["Subscription"]);
        }

        [Fact]
        public void TrackUnknownInstanceCount()
        {
            _target.TrackUnknownInstanceCount(_region, _count);

            _telemetryClient.Verify(
                x => x.TrackMetric(
                    "RebootSearchInstance.UnknownInstances",
                    _count,
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);
            Assert.NotNull(_properties);
            Assert.Equal(new[] { "Region", "Subscription" }, _properties.Keys.OrderBy(x => x));
            Assert.Equal(_region, _properties["Region"]);
            Assert.Equal(_subscription, _properties["Subscription"]);
        }

        [Fact]
        public void TrackInstanceCount()
        {
            _target.TrackInstanceCount(_region, _count);

            _telemetryClient.Verify(
                x => x.TrackMetric(
                    "RebootSearchInstance.Instances",
                    _count,
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);
            Assert.NotNull(_properties);
            Assert.Equal(new[] { "Region", "Subscription" }, _properties.Keys.OrderBy(x => x));
            Assert.Equal(_region, _properties["Region"]);
            Assert.Equal(_subscription, _properties["Subscription"]);
        }

        [Fact]
        public void TrackInstanceReboot()
        {
            _target.TrackInstanceReboot(_region, _index);

            _telemetryClient.Verify(
                x => x.TrackMetric(
                    "RebootSearchInstance.InstanceReboot",
                    1,
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);
            Assert.NotNull(_properties);
            Assert.Equal(new[] { "InstanceIndex", "Region", "Subscription" }, _properties.Keys.OrderBy(x => x));
            Assert.Equal(_index.ToString(), _properties["InstanceIndex"]);
            Assert.Equal(_region, _properties["Region"]);
            Assert.Equal(_subscription, _properties["Subscription"]);
        }

        [Fact]
        public void TrackInstanceRebootDuration()
        {
            _target.TrackInstanceRebootDuration(_region, _index, _duration, _health);

            _telemetryClient.Verify(
                x => x.TrackMetric(
                    "RebootSearchInstance.InstanceRebootDurationSeconds",
                    _duration.TotalSeconds,
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once);
            Assert.NotNull(_properties);
            Assert.Equal(new[] { "Health", "InstanceIndex", "Region", "Subscription" }, _properties.Keys.OrderBy(x => x));
            Assert.Equal("Unknown", _properties["Health"]);
            Assert.Equal(_index.ToString(), _properties["InstanceIndex"]);
            Assert.Equal(_region, _properties["Region"]);
            Assert.Equal(_subscription, _properties["Subscription"]);
        }
    }
}
