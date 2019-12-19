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

        public static DurationMetric<TProperties> TrackDuration<TProperties>(
            this ITelemetryClient telemetry,
            string metricName,
            TProperties properties,
            Func<TProperties, IDictionary<string, string>> serializeFunc)
        {
            return new DurationMetric<TProperties>(telemetry, metricName, properties, serializeFunc);
        }
    }
}