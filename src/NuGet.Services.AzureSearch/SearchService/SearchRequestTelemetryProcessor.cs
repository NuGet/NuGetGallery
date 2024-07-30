// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using NuGet.Services.Logging;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchRequestTelemetryProcessor : ITelemetryProcessor
    {
        private readonly RequestTelemetryProcessor _next;

        public SearchRequestTelemetryProcessor(ITelemetryProcessor nextProcessor)
        {
            _next = new RequestTelemetryProcessor(nextProcessor);
            _next.SuccessfulResponseCodes.Add(400);
            _next.SuccessfulResponseCodes.Add(403);
            _next.SuccessfulResponseCodes.Add(404);
            _next.SuccessfulResponseCodes.Add(405);
        }

        public void Process(ITelemetry item)
        {
            _next.Process(item);
        }
    }
}
