// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGetGallery
{
    /// <summary>
    /// Interface for the Application Insights TelemetryClient class, for unit tests.
    /// </summary>
    public interface ITelemetryClient
    {
        void TrackTrace(string message, LogLevel logLevel, EventId eventId);

        void TrackMetric(string metricName, double value, IDictionary<string, string> properties = null);

        void TrackAggregatedMetric(string metricName, double value, string dimension0Name, string dimension0Value);

        void TrackException(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null);

        void TrackDependency(string dependencyTypeName,
                             string target,
                             string dependencyName,
                             string data,
                             DateTimeOffset startTime,
                             TimeSpan duration,
                             string resultCode,
                             bool success,
                             IDictionary<string, string> properties);
    }
}