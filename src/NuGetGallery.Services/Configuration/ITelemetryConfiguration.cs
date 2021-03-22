// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Configuration
{
    public interface ITelemetryConfiguration
    {
        /// <summary>
        /// Gets the Application Insights instrumentation key associated with this deployment.
        /// </summary>
        string AppInsightsInstrumentationKey { get; set; }

        /// <summary>
        /// Gets the Application Insights sampling percentage associated with this deployment.
        /// </summary>
        double AppInsightsSamplingPercentage { get; set; }

        /// <summary>
        /// Gets the Application Insights heartbeat interval in seconds associated with this deployment.
        /// </summary>
        int AppInsightsHeartbeatIntervalSeconds { get; set; }

        /// <summary>
        /// Deployment label to log with telemetry.
        /// </summary>
        string DeploymentLabel { get; set; }
    }
}
