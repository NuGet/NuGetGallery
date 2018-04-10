// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class TelemetryHandler : DelegatingHandler
    {
        private readonly ITelemetryService _telemetryService;

        public TelemetryHandler(ITelemetryService telemetryService, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            bool success = false;
            HttpStatusCode? statusCode = null;
            long? contentLength = null;

            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                success = response.IsSuccessStatusCode;
                statusCode = response.StatusCode;
                contentLength = response.Content?.Headers?.ContentLength;

                return response;
            }
            finally
            {
                _telemetryService.TrackHttpHeaderDuration(
                    sw.Elapsed,
                    request.Method,
                    request.RequestUri,
                    success,
                    statusCode,
                    contentLength);
            }
        }
    }
}
