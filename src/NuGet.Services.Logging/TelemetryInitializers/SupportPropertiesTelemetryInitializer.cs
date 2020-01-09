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
            // We need to cast to ISupportProperties to avoid using the deprecated telemetry.Context.Properties API.
            // https://github.com/Microsoft/ApplicationInsights-Home/issues/300
            if (!(telemetry is ISupportProperties itemTelemetry))
            {
                return;
            }

            // Note that telemetry initializers can be called multiple times for the same telemetry item.
            // https://github.com/microsoft/ApplicationInsights-dotnet-server/issues/977
            if (itemTelemetry.Properties.ContainsKey(PropertyName))
            {
                return;
            }

            itemTelemetry.Properties.Add(PropertyName, PropertyValue);
        }
    }
}