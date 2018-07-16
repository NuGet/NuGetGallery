// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Revalidate
{
    public class RevalidationConfiguration
    {
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
        /// The configurations used by the in-memory queue of revalidations to start.
        /// </summary>
        public RevalidationQueueConfiguration Queue { get; set; }
    }
}
