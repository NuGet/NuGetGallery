// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.DatabaseMigrationTools
{
    internal static class MigrationTargetDatabaseArgumentNames
    {
        // avoid value duplication, avoid annoying namespace conflicts in this job
        public const string GalleryDatabase = NuGet.Jobs.JobArgumentNames.GalleryDatabase;
        // need to add these database argument names to NuGet.Jobs:
        // https://github.com/nuget/engineering/issues/2405
        public const string SupportRequestDatabase = "SupportRequestDatabase";
    }
}
