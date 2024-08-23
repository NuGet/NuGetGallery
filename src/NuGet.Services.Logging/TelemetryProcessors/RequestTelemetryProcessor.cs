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
    /// An Application Insights telemetry processor for <see cref="RequestTelemetry" />.
    /// </summary>
    public sealed class RequestTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _nextProcessor;

        /// <summary>
        /// Gets the response codes that should always be marked as successful.
        /// </summary>
        public IList<int> SuccessfulResponseCodes { get; } = new List<int>();

        /// <summary>
        /// Initialize a new <see cref="RequestTelemetryProcessor" /> class.
        /// </summary>
        /// <param name="nextProcessor">The next telemetry processor in the processing chain.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="nextProcessor" />
        /// is <c>null</c>.</exception>
        public RequestTelemetryProcessor(ITelemetryProcessor nextProcessor)
        {
            _nextProcessor = nextProcessor ?? throw new ArgumentNullException(nameof(nextProcessor));
        }

        /// <summary>
        /// Processes a telemetry item.
        /// </summary>
        /// <param name="item">A telemetry item.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="item" /> is <c>null</c>.</exception>
        public void Process(ITelemetry item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var requestTelemetry = item as RequestTelemetry;

            int responseCode;

            if (requestTelemetry != null && int.TryParse(requestTelemetry.ResponseCode, out responseCode))
            {
                if (SuccessfulResponseCodes.Contains(responseCode))
                {
                    requestTelemetry.Success = true;
                }
            }

            _nextProcessor.Process(item);
        }
    }
}