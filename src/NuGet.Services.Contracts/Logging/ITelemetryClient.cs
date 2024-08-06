// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Logging
{
    public interface ITelemetryClient
    {
        void TrackMetric(
            string metricName,
            double value,
            IDictionary<string, string> properties = null);

        void TrackMetric(
            DateTimeOffset timestamp,
            string metricName,
            double value,
            IDictionary<string, string> properties = null);

        void TrackException(
            Exception exception,
            IDictionary<string, string> properties = null,
            IDictionary<string, double> metrics = null);

        void Flush();
    }
}