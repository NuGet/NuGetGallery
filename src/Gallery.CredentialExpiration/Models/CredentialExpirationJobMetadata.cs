// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
namespace Gallery.CredentialExpiration.Models
{
    public class CredentialExpirationJobMetadata
    {
        public DateTimeOffset JobRunTime { get; }

        public JobRunTimeCursor JobCursor { get; }

        public int WarnDaysBeforeExpiration { get ; }

        public CredentialExpirationJobMetadata(DateTimeOffset jobRunTime, int warnDaysBeforeExpiration, JobRunTimeCursor jobCursor)
        {
            JobRunTime = jobRunTime;
            WarnDaysBeforeExpiration = warnDaysBeforeExpiration;
            JobCursor = jobCursor;
        }
    }
}
