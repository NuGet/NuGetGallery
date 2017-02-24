// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.SupportRequests.Notifications
{
    internal static class LogEvents
    {
        public static EventId JobRunFailed = new EventId(550, "Job run failed");
        public static EventId JobInitFailed = new EventId(551, "Job initialization failed");
    }
}