// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Gallery.CredentialExpiration
{
    public class JobRunTimeCursor
    {
        public JobRunTimeCursor(DateTimeOffset jobCursorTime, DateTimeOffset maxProcessedCredentialsTime)
        {
            JobCursorTime = jobCursorTime;
            MaxProcessedCredentialsTime = maxProcessedCredentialsTime;
        }

        public DateTimeOffset JobCursorTime { get; }

        public DateTimeOffset MaxProcessedCredentialsTime { get; }
    }
}
