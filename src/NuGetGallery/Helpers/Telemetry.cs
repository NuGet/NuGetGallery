// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;

namespace NuGetGallery
{
    internal static class Telemetry
    {
        private static readonly TelemetryClient _telemetryClient = new TelemetryClient();
        //a list of all telemetry events
        internal static Dictionary<string, string> _events = new Dictionary<string, string>() { { "QueryWhitelist", "ODataQueryWhitelist" } };

        public static void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (_telemetryClient.IsEnabled())
            {
                try
                {
                    _telemetryClient.TrackEvent(eventName, properties, metrics);
                }
                catch
                {
                    // logging failed, don't allow exception to escape
                }
            }
        }

        public static void TrackException(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (_telemetryClient.IsEnabled())
            {
                try
                {
                    _telemetryClient.TrackException(exception, properties, metrics);
                }
                catch
                {
                    // logging failed, don't allow exception to escape
                }
            }
        }
    }
}