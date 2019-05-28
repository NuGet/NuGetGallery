// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services.Telemetry
{
    public enum UserPackageDeleteOutcome
    {
        Accepted,
        AlreadyDeleted,
        LockedRegistration,
        TooManyIdDatabaseDownloads,
        TooManyIdReportDownloads,
        StaleStatistics,
        TooManyVersionDatabaseDownloads,
        TooManyVersionReportDownloads,
        TooLate,
    }
}