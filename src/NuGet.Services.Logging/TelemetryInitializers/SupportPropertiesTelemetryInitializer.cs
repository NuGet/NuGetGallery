// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    public abstract class SupportPropertiesTelemetryInitializer
        : ITelemetryInitializer
    {
        protected SupportPropertiesTelemetryInitializer(string propertyName, string propertyValue)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            PropertyValue = propertyValue ?? throw new ArgumentNullException(nameof(propertyValue));
        }

        public string PropertyName { get; }
        public string PropertyValue { get; }

        public void Initialize(ITelemetry telemetry)
        {
            if (!(telemetry is ISupportProperties itemTelemetry))
            {
                return;
            }

            if (itemTelemetry.Properties.ContainsKey(PropertyName))
            {
                return;
            }

            itemTelemetry.Properties.Add(PropertyName, PropertyValue);
        }
    }
}