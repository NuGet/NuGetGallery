// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;
using NuGet.Services.DatabaseMigration;

namespace NuGetGallery.DatabaseMigrationTools
{
    class Program
    {
        static void Main(string[] args)
        {
            var migrationContextFactory = new GalleryMigrationContextFactory();
            var job = new Job(migrationContextFactory);
            JobRunner.RunOnce(job, args).GetAwaiter().GetResult();
        }
    }
}
