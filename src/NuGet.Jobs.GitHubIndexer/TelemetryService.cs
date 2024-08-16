// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "GitHubIndexer.";
        private const string RunDuration = Prefix + "RunDurationSeconds";
        private const string DiscoverRepositoriesDuration = Prefix + "DiscoverRepositoriesDurationSeconds";
        private const string IndexRepositoryDuration = Prefix + "IndexRepositoryDurationSeconds";
        private const string ListFilesDuration = Prefix + "ListFilesDurationSeconds";
        private const string CheckOutFilesDuration = Prefix + "CheckOutFilesDurationSeconds";
        private const string UploadGitHubUsageBlobDuration = Prefix + "UploadGitHubUsageBlobDurationSeconds";
        private const string EmptyGitHubUsageBlob = Prefix + "EmptyGitHubUsageBlob";

        private const string Completed = "Completed";
        private const string RepositoryName = "RepositoryName";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackRunDuration(TimeSpan duration, bool completed)
        {
            _telemetryClient.TrackMetric(
                RunDuration,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Completed, completed.ToString() }
                });
        }

        public IDisposable TrackDiscoverRepositoriesDuration()
        {
            return _telemetryClient.TrackDuration(DiscoverRepositoriesDuration);
        }

        public IDisposable TrackIndexRepositoryDuration(string repositoryName)
        {
            return _telemetryClient.TrackDuration(
                IndexRepositoryDuration,
                new Dictionary<string, string>
                {
                    { RepositoryName, repositoryName}
                });
        }

        public IDisposable TrackListFilesDuration(string repositoryName)
        {
            return _telemetryClient.TrackDuration(
                ListFilesDuration,
                new Dictionary<string, string>
                {
                    { RepositoryName, repositoryName}
                });
        }

        public IDisposable TrackCheckOutFilesDuration(string repositoryName)
        {
            return _telemetryClient.TrackDuration(
                CheckOutFilesDuration,
                new Dictionary<string, string>
                {
                    { RepositoryName, repositoryName}
                });
        }

        public IDisposable TrackUploadGitHubUsageBlobDuration()
        {
            return _telemetryClient.TrackDuration(UploadGitHubUsageBlobDuration);
        }

        public void TrackEmptyGitHubUsageBlob()
        {
            _telemetryClient.TrackMetric(EmptyGitHubUsageBlob, 1);
        }
    }
}
