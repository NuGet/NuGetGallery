// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsService : IDiagnosticsService
    {
        private readonly ITelemetryClient _telemetryClient;

        public DiagnosticsService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public IDiagnosticsSource GetSource(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ServicesStrings.ParameterCannotBeNullOrEmpty,
                        "name"),
                    nameof(name));
            }

            return new TraceDiagnosticsSource(name, _telemetryClient);
        }
    }
}