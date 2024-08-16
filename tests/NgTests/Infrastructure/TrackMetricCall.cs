// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NgTests.Infrastructure
{
    public sealed class TrackMetricCall : TelemetryCall
    {
        public ulong Metric { get; }

        internal TrackMetricCall(string name, ulong metric, IDictionary<string, string> properties)
            : base(name, properties)
        {
            Metric = metric;
        }
    }
}