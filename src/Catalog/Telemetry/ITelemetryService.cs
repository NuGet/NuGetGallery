// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog
{
    public interface ITelemetryService
    {
        void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri);

        void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri);

        /// <summary>
        /// Tracks the duration to fetch the HTTP response headers. Note that this duration does not include the time
        /// it takes to fetch response body. In other words, this metric is not interesting for bandwidth analysis.
        /// </summary>
        void TrackHttpHeaderDuration(
            TimeSpan duration,
            HttpMethod method,
            Uri uri,
            bool success,
            HttpStatusCode? statusCode,
            long? contentLength);
    }
}