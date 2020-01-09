// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    public class JobPropertiesTelemetryInitializer
         : ITelemetryInitializer
    {
        private readonly string _jobName;
        private readonly string _instanceName;
        private readonly IDictionary<string, string> _globalDimensions;

        public JobPropertiesTelemetryInitializer(
            string jobName,
            string instanceName,
            IDictionary<string, string> globalDimensions)
        {
            _jobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
            _instanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
            _globalDimensions = globalDimensions ?? throw new ArgumentNullException(nameof(globalDimensions));
        }

        public void Initialize(ITelemetry telemetry)
        {
            // We need to cast to ISupportProperties to avoid using the deprecated telemetry.Context.Properties API.
            // https://github.com/Microsoft/ApplicationInsights-Home/issues/300
            if (!(telemetry is ISupportProperties itemTelemetry))
            {
                return;
            }

            itemTelemetry.Properties["JobName"] = _jobName;
            itemTelemetry.Properties["InstanceName"] = _instanceName;

            foreach (var dimension in _globalDimensions)
            {
                itemTelemetry.Properties[dimension.Key] = dimension.Value;
            }
        }
    }
}
