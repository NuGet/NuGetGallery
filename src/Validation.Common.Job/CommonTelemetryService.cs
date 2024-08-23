// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation
{
    public class CommonTelemetryService : ICommonTelemetryService
    {
        private const string FileDownloadedSeconds = "FileDownloadedSeconds";
        private const string FileDownloadSpeed = "FileDownloadSpeedBytesPerSec";
        private const string FileUri = "FileUri";
        private const string FileSize = "FileSize";
        private const double DefaultDownloadSpeed = 1;

        protected readonly ITelemetryClient _telemetryClient;

        public CommonTelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackFileDownloaded(Uri fileUri, TimeSpan duration, long size)
        {
            // Remove the query string from the file URI, since this could contain a SAS token.
            var uriBuilder = new UriBuilder(fileUri);
            uriBuilder.Query = null;
            var absoluteUri = uriBuilder.Uri.AbsoluteUri;

            _telemetryClient.TrackMetric(
                FileDownloadedSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { FileUri, absoluteUri },
                    { FileSize, size.ToString() },
                });
            _telemetryClient.TrackMetric(
                FileDownloadSpeed,
                duration.TotalSeconds > 0 ? size / duration.TotalSeconds : DefaultDownloadSpeed,
                new Dictionary<string, string>
                {
                    { FileUri, absoluteUri },
                    { FileSize, size.ToString() },
                });
        }
    }
}
