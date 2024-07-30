// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace CopyAzureContainer
{
    public class LogEvents
    {
        //reserve 700+ event ids for this job
        public static EventId JobRunFailed = new EventId(700, "Job run failed");
        public static EventId JobInitFailed = new EventId(701, "Job initialization failed");
        public static EventId CreateContainerFailed = new EventId(702, "Create container failed");
        public static EventId DeleteContainerFailed = new EventId(703, "Delete container failed");
        public static EventId CopyContainerFailed = new EventId(704, "Copy container failed");
        public static EventId CopyLogFailed = new EventId(705, "Copy log failed");
    }
}
