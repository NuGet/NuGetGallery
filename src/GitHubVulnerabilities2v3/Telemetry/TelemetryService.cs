// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Logging;
using System;

namespace GitHubVulnerabilities2v3.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "GitHubVulnerability2v3.";
        private const string SpecialCaseTrigger = Prefix + "SpecialCaseTrigger";
        private const string UpdateRun = Prefix + "UpdateRun";
        private const string RegenerationRun = Prefix + "RegenerationRun";
        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackSpecialCaseTrigger()
        {
            _telemetryClient.TrackMetric(
                SpecialCaseTrigger,
                1);
        }

        public void TrackUpdateRun(int vulnerabilityCount)
        {
            _telemetryClient.TrackMetric(UpdateRun, vulnerabilityCount);
        }

        public void TrackRegenerationRun(int vulnerabilityCount)
        {
            _telemetryClient.TrackMetric(RegenerationRun, vulnerabilityCount);
        }
    }
}
