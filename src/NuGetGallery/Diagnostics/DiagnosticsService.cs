// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using NuGetGallery.Services.Telemetry;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsService : IDiagnosticsService
    {
        private readonly ITelemetryClient _telemetryClient;

        public DiagnosticsService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            Trace.AutoFlush = true;
        }

        // Test constructor
        internal DiagnosticsService() : this(TelemetryClientWrapper.Instance)
        {
        }

        public IDiagnosticsSource GetSource(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, "name"), nameof(name));
            }

            return new TraceDiagnosticsSource(name, _telemetryClient);
        }
    }
}