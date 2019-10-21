// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using NuGet.Services.Metadata.Catalog;

namespace Ng
{
    public class JobPropertiesTelemetryInitializer : ITelemetryInitializer
    {
        private readonly ITelemetryService _telemetryService;

        public JobPropertiesTelemetryInitializer(ITelemetryService telemetryService)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (_telemetryService.GlobalDimensions != null)
            {
                var supportProperties = (ISupportProperties)telemetry;

                foreach (var dimension in _telemetryService.GlobalDimensions)
                {
                    supportProperties.Properties[dimension.Key] = dimension.Value;
                }
            }
        }
    }
}