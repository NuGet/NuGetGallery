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
        void TrackHttpDuration(TimeSpan duration, HttpMethod method, Uri uri, HttpStatusCode statusCode, bool success);
    }
}