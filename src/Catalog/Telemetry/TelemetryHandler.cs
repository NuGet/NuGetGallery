// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            var properties = new Dictionary<string, string>()
            {
                { TelemetryConstants.Method, request.Method.ToString() },
                { TelemetryConstants.Uri, request.RequestUri.AbsoluteUri }
            };

            using (_telemetryService.TrackDuration(TelemetryConstants.HttpHeaderDurationSeconds, properties))
            {
                var response = await base.SendAsync(request, cancellationToken);

                var contentLength = response.Content?.Headers?.ContentLength;

                properties[TelemetryConstants.StatusCode] = ((int)response.StatusCode).ToString();
                properties[TelemetryConstants.Success] = response.IsSuccessStatusCode.ToString();
                properties[TelemetryConstants.ContentLength] = contentLength == null ? "0" : contentLength.ToString();

                return response;
            }
        }
    }
}
