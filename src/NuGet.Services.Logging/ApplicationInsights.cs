// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    public static class ApplicationInsights
    {
        public static bool Initialized { get; private set; }

        public static void Initialize(string instrumentationKey)
        {
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;
                TelemetryConfiguration.Active.TelemetryInitializers.Add(new TelemetryContextInitializer());

                Initialized = true;
            }
            else
            {
                Initialized = false;
            }
        }
    }
}