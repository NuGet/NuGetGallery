// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Logging;
using System;
using System.Collections.Generic;

namespace NuGet.Jobs.Monitoring.PackageLag.Telemetry
{
    public class PackageLagTelemetryService : IPackageLagTelemetryService
    {
        private readonly ITelemetryClient _telemetryClient;

        private const string PackageId = "PackageId";
        private const string PackageVersion = "Version";
        private const string Region = "Region";
        private const string Subscription = "Subscription";
        private const string InstanceIndex = "InstanceIndex";
        private const string ServiceType = "ServiceType";

        private const string CreatedLagName = "PackageCreationLagInSeconds";
        private const string V3LagName = "V3LagInSeconds";

        public PackageLagTelemetryService(ITelemetryClient telemetryClient, PackageLagMonitorConfiguration configuration)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackPackageCreationLag(DateTimeOffset eventTime, Instance instance, string packageId, string packageVersion, TimeSpan createdDelay)
        {
            _telemetryClient.TrackMetric(CreatedLagName, createdDelay.TotalSeconds, new Dictionary<string, string>
            {
                { PackageId, packageId },
                { PackageVersion, packageVersion },
                { Region, instance.Region },
                { InstanceIndex, instance.Index.ToString() },
                { ServiceType, instance.ServiceType.ToString() }
            });
        }

        public void TrackV3Lag(DateTimeOffset eventTime, Instance instance, string packageId, string packageVersion, TimeSpan v3Delay)
        {
            _telemetryClient.TrackMetric(V3LagName, v3Delay.TotalSeconds, new Dictionary<string, string>
            {
                { PackageId, packageId },
                { PackageVersion, packageVersion },
                { Region,  instance.Region },
                { InstanceIndex, instance.Index.ToString() },
                { ServiceType, instance.ServiceType.ToString() }
            });
        }
    }
}
