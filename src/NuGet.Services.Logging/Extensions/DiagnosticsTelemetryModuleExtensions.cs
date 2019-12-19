// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace NuGet.Services.Logging
{
    public static class DiagnosticsTelemetryModuleExtensions
    {
        /// <summary>
        /// Tries to add or set a heartbeat property.
        /// </summary>
        /// <param name="module">The <see cref="DiagnosticsTelemetryModule"/>.</param>
        /// <param name="propertyName">Name of the heartbeat value to add.</param>
        /// <param name="propertyValue">Current value of the heartbeat value to add.</param>
        /// <param name="isHealthy">Flag indicating whether or not the property represents a healthy value.</param>
        /// <returns><c>True</c> if the property was set; otherwise <c>False</c>.</returns>
        public static bool AddOrSetHeartbeatProperty(
            this DiagnosticsTelemetryModule module,
            string propertyName,
            string propertyValue,
            bool isHealthy)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            var propertySet = module.AddHeartbeatProperty(propertyName, propertyValue, isHealthy);
            if (!propertySet)
            {
                return module.SetHeartbeatProperty(propertyName, propertyValue, isHealthy);
            }

            return propertySet;
        }
    }
}