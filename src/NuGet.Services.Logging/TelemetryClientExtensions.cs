// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Logging
{
    public static class TelemetryClientExtensions
    {
        public static IDisposable TrackDuration(
            this ITelemetryClient telemetry,
            string metricName,
            IDictionary<string, string> properties = null)
        {
            return new DurationMetric(telemetry, metricName, properties);
        }
    }
}