// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation.Common
{
    public static class TraceEvent
    {
        public static readonly EventId ValidatorException = CreateEventId(0, "Validator exception");
        public static readonly EventId CommandLineProcessingFailed = CreateEventId(1, "Failed to process Job's command line arguments");
        public static readonly EventId StartValidationAuditFailed = CreateEventId(2, "Failed to save audit info regarding validation queueing");

        /// <summary>
        /// Random number used as a base for EventIds and to make sure they don't clash with 
        /// other Ids across the project.
        /// </summary>
        private const int StartId = 1186511685;
        private static EventId CreateEventId(int offset, string name)
        {
            return new EventId(StartId + offset, name);
        }
    }
}
