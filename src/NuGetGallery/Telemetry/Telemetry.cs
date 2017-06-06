// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;

namespace NuGetGallery
{
    internal static class Telemetry
    {
        static Telemetry()
        {
            TelemetryClient = new TelemetryClient();
        }

        internal static TelemetryClient TelemetryClient { get; }

        public static void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                TelemetryClient.TrackEvent(eventName, properties, metrics);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public static void TrackException(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                TelemetryClient.TrackException(exception, properties, metrics);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}