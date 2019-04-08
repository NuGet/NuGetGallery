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
        private const string InstanceNameKey = "InstanceName";

        private readonly string _jobName;
        private readonly string _instanceName;

        public JobNameTelemetryInitializer(string jobName, string instanceName)
        {
            _jobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
            _instanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
        }

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Properties[JobNameKey] = _jobName;
            telemetry.Context.Properties[InstanceNameKey] = _instanceName;
        }
    }
}