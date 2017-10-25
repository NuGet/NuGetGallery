// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;

namespace NuGetGallery
{
    /// <summary>
    /// Wrapper for the Application Insights TelemetryClient class.
    /// </summary>
    public class TelemetryClientWrapper : ITelemetryClient
    {
        internal static TelemetryClientWrapper Instance = new TelemetryClientWrapper();

        private TelemetryClientWrapper()
        {
            UnderlyingClient = new TelemetryClient();
        }

        internal TelemetryClient UnderlyingClient { get; }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                UnderlyingClient.TrackEvent(eventName, properties, metrics);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public void TrackException(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                UnderlyingClient.TrackException(exception, properties, metrics);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}