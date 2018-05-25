// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using NuGet.Services.Logging;

namespace NuGet.Services.Metadata.Catalog
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClientWrapper _telemetryClient;

        public IDictionary<string, string> GlobalDimensions { get; }

        public TelemetryService(TelemetryClient telemetryClient)
        {
            if (telemetryClient == null)
            {
                throw new ArgumentNullException(nameof(telemetryClient));
            }

            _telemetryClient = new TelemetryClientWrapper(telemetryClient);
            GlobalDimensions = new Dictionary<string, string>();
        }

        public void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.CatalogIndexReadDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Uri, uri.AbsoluteUri },
                });
        }

        public void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.CatalogIndexWriteDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Uri, uri.AbsoluteUri },
                });
        }

        public void TrackMetric(string name, ulong metric, IDictionary<string, string> properties = null)
        {
            _telemetryClient.TrackMetric(name, metric, properties);
        }

        public virtual DurationMetric TrackDuration(string name, IDictionary<string, string> properties = null)
        {
            return new DurationMetric(_telemetryClient, name, properties);
        }
    }
}