// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.BasicSearch
{
    public class SearchTelemetryClient
    {
        internal TelemetryClient TelemetryClient { get; }

        public static class MetricName
        {
            public const string SearchIndexReopenDuration = "SearchIndexReopenDuration";
            public const string SearchIndexReopenFailed = "SearchIndexReopenFailed";
            public const string SearcherManagerNotInitialized = "SearcherManagerNotInitialized";

            public const string LuceneNumDocs = "LuceneNumDocs";
            public const string LuceneLoadLag = "LuceneLoadLag";
            public const string LuceneLastReopen = "LuceneLastReopen";
            public const string LuceneCommitTimestamp = "LuceneCommitTimestamp";
        }

        public SearchTelemetryClient(TelemetryConfiguration telemetryConfiguration)
        {
            TelemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        public void TrackMetric(string name, double value, IDictionary<string, string> properties = null)
        {
            TelemetryClient.TrackMetric(name, value, properties);
        }
    }
}