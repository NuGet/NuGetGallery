// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace SnapshotAzureBlob
{
    public class LogEvents
    {
        //reserve 800+ event ids for this job
        public static EventId JobRunFailed = new EventId(800, "Job run failed");
        public static EventId JobInitFailed = new EventId(801, "Job initialization failed");
        public static EventId SnaphotFailed = new EventId(802, "Snapshot failed");
    }
}
