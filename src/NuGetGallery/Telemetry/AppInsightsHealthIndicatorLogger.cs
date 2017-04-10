// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel;
using Microsoft.ApplicationInsights;
using NuGet.Services.Search.Client;

namespace NuGetGallery
{
    internal class AppInsightsHealthIndicatorLogger
        : IHealthIndicatorLogger
    {
        public void LogDecreaseHealth(Uri endpoint, int health, Exception exception)
        {
            var telemetryClient = new TelemetryClient();
            telemetryClient.TrackEvent("Endpoint Health Changed",
                new Dictionary<string, string>()
                {
                    {"Endpoint", endpoint.ToString()},
                    {"Event", "Decrease"}
                }, new Dictionary<string, double>()
                {
                    {"Endpoint Health", health}
                });

            QuietLog.LogHandledException(exception);
        }

        public void LogIncreaseHealth(Uri endpoint, int health)
        {
            var telemetryClient = new TelemetryClient();
            telemetryClient.TrackEvent("Endpoint Health Changed",
                new Dictionary<string, string>()
                {
                    {"Endpoint", endpoint.ToString()},
                    {"Event", "Increase"}
                }, new Dictionary<string, double>()
                {
                    {"Endpoint Health", health}
                });
        }
    }
}