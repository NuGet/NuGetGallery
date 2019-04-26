// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.DatabaseMigrationTools
{
    internal static class JobArgumentNames
    {
        public const string MigrationTargetDatabase = "MigrationTargetDatabase";

        // avoids value duplication, avoids annoying namespace conflicts in this job
        public const string GalleryDatabase = NuGet.Jobs.JobArgumentNames.GalleryDatabase;
        // need to add these database argument names to NuGet.Jobs
        public const string SupportRequestDatabase = "SupportRequestDatabase";
        public const string ValidationDatabase = "ValidationDatabase";
    }
}
