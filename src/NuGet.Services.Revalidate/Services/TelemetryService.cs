// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace NuGet.Services.Revalidate
{
    public class TelemetryService : ITelemetryService
    {
        private const string RevalidationPrefix = "Revalidation.";

        private const string DurationToStartNextRevalidation = RevalidationPrefix + "DurationToStartNextRevalidation";
        private const string PackageRevalidationMarkedAsCompleted = RevalidationPrefix + "PackageRevalidationMarkedAsCompleted";
        private const string PackageRevalidationStarted = RevalidationPrefix + "PackageRevalidationStarted";

        private const string PackageId = "PackageId";
        private const string NormalizedVersion = "NormalizedVersion";

        private readonly ITelemetryClient _client;

        public TelemetryService(ITelemetryClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IDisposable TrackDurationToStartNextRevalidation()
        {
            return _client.TrackDuration(DurationToStartNextRevalidation);
        }

        public void TrackPackageRevalidationMarkedAsCompleted(string packageId, string normalizedVersion)
        {
            _client.TrackMetric(
                PackageRevalidationMarkedAsCompleted,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                });
        }

        public void TrackPackageRevalidationStarted(string packageId, string normalizedVersion)
        {
            _client.TrackMetric(
                PackageRevalidationStarted,
                1,
                new Dictionary<string, string>
                {
                    { PackageId, packageId },
                    { NormalizedVersion, normalizedVersion },
                });
        }
    }
}
