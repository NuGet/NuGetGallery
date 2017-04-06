// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace NuGetGallery.Helpers
{
    public class TelemetryResponseCodeFilter : ITelemetryProcessor
    {
        public TelemetryResponseCodeFilter(ITelemetryProcessor next)
        {
            Next = next;
        }
        
        private ITelemetryProcessor Next { get; set; }

        public void Process(ITelemetry item)
        {
            var request = item as RequestTelemetry;
            int responseCode;

            if (request != null && int.TryParse(request.ResponseCode, out responseCode))
            {
                if (responseCode == 400 || responseCode == 404)
                {
                    request.Success = true;
                }
            }

            this.Next.Process(item);
        }
    }
}