// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class LogEvents
    {
        public static EventId ValidationFailed = new EventId(900, "Validation failed!");
        public static EventId ValidationFailedToRun = new EventId(901, "Failed to run validation!");
        public static EventId ValidationFailedToInitialize = new EventId(902, "Failed to initialize validation!");

        public static EventId StatusDeserializationFailure = new EventId(903, "Status deserialization failed!");
        public static EventId StatusDeserializationFatalFailure = new EventId(904, "Status deserialization failed, and was unable to parse id and version from filename!");

        public static EventId QueueMessageFatalFailure = new EventId(905, "Failed to process queue message");
        public static EventId QueueMessageRemovalFailure = new EventId(906, "Failed to remove queue message");
    }
}
