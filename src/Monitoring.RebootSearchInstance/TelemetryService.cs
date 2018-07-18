// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using NuGet.Services.Logging;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class TelemetryService : ITelemetryService
    {
        private const string Region = "Region";
        private const string Subscription = "Subscription";
        private const string InstanceIndex = "InstanceIndex";
        private const string Health = "Health";

        private const string Prefix = "RebootSearchInstance.";
        private const string HealthyInstanceCount = Prefix + "HealthyInstances";
        private const string UnhealthyInstanceCount = Prefix + "UnhealthyInstances";
        private const string UnknownInstanceCount = Prefix + "UnknownInstances";
        private const string InstanceCount = Prefix + "Instances";
        private const string InstanceReboot = Prefix + "InstanceReboot";
        private const string InstanceRebootDuration = Prefix + "InstanceRebootDurationSeconds";

        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptionsSnapshot<MonitorConfiguration> _configuration;
        private readonly string _subscription;

        public TelemetryService(ITelemetryClient telemetryClient, IOptionsSnapshot<MonitorConfiguration> configuration)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _subscription = _configuration.Value.Subscription;
        }

        public void TrackHealthyInstanceCount(string region, int count)
        {
            TrackInstanceCount(HealthyInstanceCount, region, count);
        }

        public void TrackUnhealthyInstanceCount(string region, int count)
        {
            TrackInstanceCount(UnhealthyInstanceCount, region, count);
        }

        public void TrackUnknownInstanceCount(string region, int count)
        {
            TrackInstanceCount(UnknownInstanceCount, region, count);
        }

        public void TrackInstanceCount(string region, int count)
        {
            TrackInstanceCount(InstanceCount, region, count);
        }

        public void TrackInstanceReboot(string region, int index)
        {
            _telemetryClient.TrackMetric(InstanceReboot, 1, new Dictionary<string, string>
            {
                { Subscription, _subscription },
                { Region, region },
                { InstanceIndex, index.ToString() },
            });
        }

        public void TrackInstanceRebootDuration(
            string region,
            int index,
            TimeSpan duration,
            InstanceHealth health)
        {
            _telemetryClient.TrackMetric(InstanceRebootDuration, duration.TotalSeconds, new Dictionary<string, string>
            {
                { Subscription, _subscription },
                { Region, region },
                { InstanceIndex, index.ToString() },
                { Health, health.ToString() },
            });
        }

        private void TrackInstanceCount(string name, string region, int count)
        {
            _telemetryClient.TrackMetric(name, count, new Dictionary<string, string>
            {
                { Subscription, _subscription },
                { Region, region },
            });
        }
    }
}
