// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.Notifications
{
    internal static class JobArgumentNames
    {
        public const string ScheduledTask = "Task";

        // avoids value duplication, avoids annoying namespace conflicts in this job
        public const string InstrumentationKey = Jobs.JobArgumentNames.InstrumentationKey;
        public const string SourceDatabase = Jobs.JobArgumentNames.SourceDatabase;
    }
}