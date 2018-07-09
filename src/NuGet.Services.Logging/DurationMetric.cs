// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Services.Logging
{
    /// <summary>
    /// An object that records the duration of its existence.
    /// </summary>
    public class DurationMetric : IDisposable
    {
        private readonly ITelemetryClient _telemetry;

        private readonly string _name;
        private readonly IDictionary<string, string> _properties;
        private readonly Stopwatch _timer;

        public DurationMetric(ITelemetryClient telemetry, string name, IDictionary<string, string> properties = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            _name = name;
            _properties = properties;
            _timer = Stopwatch.StartNew();
        }

        public void Dispose() => _telemetry.TrackMetric(_name, _timer.Elapsed.TotalSeconds, _properties);
    }

    /// <summary>
    /// An object that records the duration of its existence.
    /// </summary>
    public class DurationMetric<TProperties> : IDisposable
    {
        private readonly ITelemetryClient _telemetry;

        private readonly string _name;
        private readonly Stopwatch _timer;

        private readonly Func<TProperties, IDictionary<string, string>> _serializeFunc;

        public DurationMetric(
            ITelemetryClient telemetry,
            string name,
            TProperties properties,
            Func<TProperties, IDictionary<string, string>> serializeFunc)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _serializeFunc = serializeFunc ?? throw new ArgumentNullException(nameof(serializeFunc));

            _name = name;
            _timer = Stopwatch.StartNew();

            Properties = properties;
        }

        public TProperties Properties { get; }

        public void Dispose() => _telemetry.TrackMetric(_name, _timer.Elapsed.TotalSeconds, _serializeFunc(Properties));
    }
}
