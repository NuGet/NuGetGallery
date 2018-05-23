// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;

namespace NgTests.Infrastructure
{
    internal sealed class MockTelemetryService : ITelemetryService
    {
        public IDictionary<string, string> GlobalDimensions => throw new NotImplementedException();

        public void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri)
        {
        }

        public void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri)
        {
        }

        public DurationMetric TrackDuration(string name, IDictionary<string, string> properties = null)
        {
            return new DurationMetric(Mock.Of<ITelemetryClient>(), name, properties);
        }

        public void TrackMetric(string name, ulong metric, IDictionary<string, string> properties = null)
        {
        }
    }
}