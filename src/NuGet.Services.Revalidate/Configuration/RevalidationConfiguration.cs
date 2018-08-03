// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Revalidate
{
    public class RevalidationConfiguration
    {
        /// <summary>
        /// The lower limit for the desired package event rate (includes package pushes, lists, unlists, and revalidations).
        /// If the ingestion pipeline remains healthy, the job will increase its rate over time. If the ingestion pipeline becomes
        /// unhealthy, the job will reset its rate to this value.
        /// </summary>
        public int MinPackageEventRate { get; set; }

        /// <summary>
        /// The revalidation job will speed up over time. This is the upper limit for the desired package event
        /// rate (includes package pushes, lists, unlists, and revalidations).
        /// </summary>
        public int MaxPackageEventRate { get; set; }

        /// <summary>
        /// The time before the revalidation job restarts itself.
        /// </summary>
        public TimeSpan ShutdownWaitInterval { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// How long the revalidation job should wait if a revalidation cannot be processed at this time.
        /// </summary>
        public TimeSpan RetryLaterSleep { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The configurations used to initialize the revalidation state.
        /// </summary>
        public InitializationConfiguration Initialization { get; set; }

        /// <summary>
        /// The configurations used to determine the health of the ingestion pipeline.
        /// </summary>
        public HealthConfiguration Health { get; set; }

        /// <summary>
        /// The configurations to authenticate to Application Insight's REST endpoints.
        /// </summary>
        public ApplicationInsightsConfiguration AppInsights { get; set; }

        /// <summary>
        /// The configurations used by the in-memory queue of revalidations to start.
        /// </summary>
        public RevalidationQueueConfiguration Queue { get; set; }
    }
}
