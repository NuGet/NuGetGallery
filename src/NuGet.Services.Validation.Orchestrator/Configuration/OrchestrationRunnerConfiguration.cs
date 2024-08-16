// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation.Orchestrator
{
    public class OrchestrationRunnerConfiguration
    {
        /// <summary>
        /// The time period after which orchestration process is gracefully shut down (so it is restarted by the startup script).
        /// </summary>
        public TimeSpan ProcessRecycleInterval { get; set; }

        /// <summary>
        /// Graceful shutdown timeout: running threads would be given the specified amount of time to finish processing,
        /// if not done by the interval end, process would be terminated forcefully with log record about the incident.
        /// </summary>
        public TimeSpan ShutdownWaitInterval { get; set; }

        /// <summary>

        /// Information used by the initialization to register the correct IMessageHandler to be used by the Orchestrator.
        /// /// </summary>
        public ValidatingType ValidatingType { get; set; }

        /// Max number of concurrent calls to be handled by the service bus library
        /// </summary>
        public int MaxConcurrentCalls { get; set; }
    }
}
