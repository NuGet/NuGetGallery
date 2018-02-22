// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Ng
{
    public class JobNameTelemetryInitializer : ITelemetryInitializer
    {
        private const string JobNameKey = "JobName";

        private readonly string _jobName;

        public JobNameTelemetryInitializer(string jobName)
        {
            _jobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
        }

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Properties[JobNameKey] = _jobName;
        }
    }
}
