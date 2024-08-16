// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Gallery.CredentialExpiration
{
    public class LogEvents
    {
        public static EventId FailedToHandleExpiredCredential = new EventId(600, "Failed to handle expired credential");
        public static EventId FailedToSendMail = new EventId(601, "Failed to deliver email");
        public static EventId JobRunFailed = new EventId(650, "Job run failed");
        public static EventId JobInitFailed = new EventId(651, "Job initialization failed");
    }
}