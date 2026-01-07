// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace GitHubVulnerabilities2v3.Telemetry
{
    public interface ITelemetryService
    {
        /// <summary>
        /// Track that a special regenerate was triggered
        /// See ShouldRegenerateForSpecialCase in BlobStorageVulnerabilityWriter for more details.
        /// Ideally, this will be removed.
        /// </summary>
        void TrackSpecialCaseTrigger();

        /// <summary>
        /// Track an update run of the job and how many vulnerabilities were put in update.
        /// </summary>
        /// <param name="vulnerabilityCount">Number of vulnerabilities added in update</param>
        void TrackUpdateRun(int vulnerabilityCount);

        /// <summary>
        /// Track a regeneration run of the job and how many vulnerabilities were put written.
        /// </summary>
        /// <param name="vulnerabilityCount">Number of vulnerabilities added</param>
        void TrackRegenerationRun(int vulnerabilityCount);
    }
}
