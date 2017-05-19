// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace NuGet.Services.Logging
{
    /// <summary>
    /// Application Insights inspects a request's response code to decide if the operation
    /// was successful or not. This processor can be used to override the default behavior.
    /// </summary>
    public class TelemetryResponseCodeProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;

        /// <summary>
        /// The response codes that should always be marked as successful.
        /// </summary>
        public int[] SuccessfulResponseCodes { get; set; }

        public TelemetryResponseCodeProcessor(ITelemetryProcessor next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public void Process(ITelemetry item)
        {
            var request = item as RequestTelemetry;
            int responseCode;

            if (request != null && int.TryParse(request.ResponseCode, out responseCode))
            {
                if (SuccessfulResponseCodes.Contains(responseCode))
                {
                    request.Success = true;
                }
            }

            _next.Process(item);
        }
    }
}