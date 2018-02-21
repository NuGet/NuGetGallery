// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.ApplicationInsights;

namespace NuGet.Services.Metadata.Catalog
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClient _telemetryClient;

        private const string HttpDurationSeconds = "HttpDurationSeconds";
        private const string Method = "Method";
        private const string Uri = "Uri";
        private const string StatusCode = "StatusCode";
        private const string Success = "Success";

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackHttpDuration(TimeSpan duration, HttpMethod method, Uri uri, HttpStatusCode statusCode, bool success)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                HttpDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Method, method.ToString() },
                    { Uri, uri.AbsoluteUri },
                    { StatusCode, ((int)statusCode).ToString() },
                    { Success, success.ToString() },
                });
        }
    }
}
