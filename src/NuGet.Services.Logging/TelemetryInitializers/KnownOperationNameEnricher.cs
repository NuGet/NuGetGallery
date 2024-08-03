// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    /// <summary>
    /// Because we have web.config based redirects and other steps in the pipeline that act before ASP.NET determines
    /// the controller and action that a URL is associated with, the "operation name" field has an extremely high
    /// cardinality which makes it inappropriate as a dimension in some metric systems. For example, sometimes the
    /// operation name is the verbatim URL path, with parameters filled in. Therefore, we have a list of known operation
    /// names that we copy to another field, which is better for aggregation.
    /// </summary>
    public class KnownOperationNameEnricher : ITelemetryInitializer
    {
        private const string KnownOperation = "KnownOperation";
        private readonly HashSet<string> _knownOperations;

        public KnownOperationNameEnricher(IEnumerable<string> knownOperations)
        {
            if (knownOperations == null)
            {
                throw new ArgumentNullException(nameof(knownOperations));
            }

            _knownOperations = new HashSet<string>(knownOperations);
        }

        public void Initialize(ITelemetry telemetry)
        {
            var request = telemetry as RequestTelemetry;
            if (request == null)
            {
                return;
            }

            var itemTelemetry = telemetry as ISupportProperties;
            if (itemTelemetry == null)
            {
                return;
            }

            var operationName = telemetry.Context?.Operation?.Name;
            if (operationName == null)
            {
                return;
            }

            if (_knownOperations.Contains(operationName))
            {
                itemTelemetry.Properties[KnownOperation] = operationName;
            }
        }
    }
}
