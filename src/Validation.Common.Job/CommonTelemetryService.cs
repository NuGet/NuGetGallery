// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation
{
    public class CommonTelemetryService : ICommonTelemetryService
    {
        private const string PackageDownloadedSeconds = "PackageDownloadedSeconds";
        private const string PackageUri = "PackageUri";
        private const string PackageSize = "PackageSize";

        private readonly ITelemetryClient _telemetryClient;

        public CommonTelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackPackageDownloaded(Uri packageUri, TimeSpan duration, long size)
        {
            // Remove the query string from the package URI, since this could contain a SAS token.
            var uriBuilder = new UriBuilder(packageUri);
            uriBuilder.Query = null;
            var absoluteUri = uriBuilder.Uri.AbsoluteUri;

            _telemetryClient.TrackMetric(
                PackageDownloadedSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { PackageUri, absoluteUri },
                    { PackageSize, size.ToString() },
                });
        }
    }
}
