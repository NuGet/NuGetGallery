// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace NuGet.Services.Metadata.Catalog
{
    public interface ITelemetryService
    {
        /// <summary>
        /// Allows setting dimensions that will be added to all telemetry emited by the job.
        /// </summary>
        IDictionary<string, string> GlobalDimensions { get; }

        void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri);
        void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri);
        void TrackMetric(string name, ulong metric, IDictionary<string, string> properties = null);
        DurationMetric TrackDuration(string name, IDictionary<string, string> properties = null);
    }
}