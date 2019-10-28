// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
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
            // Note that telemetry initializers can be called multiple times for the same telemetry item, so
            // these operations need to not fail if called again. In this particular case, Dictionary.Add
            // cannot be used since it will fail if the key already exists.
            // https://github.com/microsoft/ApplicationInsights-dotnet-server/issues/977

            // We need to cast to ISupportProperties to avoid using the deprecated telemetry.Context.Properties API.
            // https://github.com/Microsoft/ApplicationInsights-Home/issues/300

            var supportProperties = (ISupportProperties)telemetry;

            supportProperties.Properties[JobNameKey] = _jobName;
            supportProperties.Properties[InstanceNameKey] = _instanceName;
        }
    }
}