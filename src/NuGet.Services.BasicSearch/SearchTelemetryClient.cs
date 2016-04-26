// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.BasicSearch
{
    public class SearchTelemetryClient
    {
        private readonly TelemetryClient _telemetryClient;

        public static class MetricName
        {
            public static readonly string SearchIndexReopenDuration = "SearchIndexReopenDuration";
            public static readonly string SearchIndexReopenFailed = "SearchIndexReopenFailed";
            public static readonly string SearcherManagerNotInitialized = "SearcherManagerNotInitialized";

            public static readonly string LuceneNumDocs = "LuceneNumDocs";
            public static readonly string LuceneLoadLag = "LuceneLoadLag";
            public static readonly string LuceneLastReopen = "LuceneLastReopen";
            public static readonly string LuceneCommitTimestamp = "LuceneCommitTimestamp";
        }

        public SearchTelemetryClient()
        {
            _telemetryClient = new TelemetryClient(TelemetryConfiguration.Active);
        }

        public void TrackMetric(string name, double value, IDictionary<string, string> properties = null)
        {
            _telemetryClient.TrackMetric(name, value, properties);
        }
    }
}