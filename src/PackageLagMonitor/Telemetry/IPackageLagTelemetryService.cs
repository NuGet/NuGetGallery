// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.PackageLag.Telemetry
{
    public interface IPackageLagTelemetryService
    {
        void TrackPackageCreationLag(DateTimeOffset eventTime, Instance instance, string packageId, string packageVersion, TimeSpan createdDelay);
        void TrackV3Lag(DateTimeOffset eventTime, Instance instance, string packageId, string packageVersion, TimeSpan v3Delay);
    }
}
