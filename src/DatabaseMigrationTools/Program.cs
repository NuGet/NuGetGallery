// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;
using NuGet.Services.DatabaseMigration;
using System.Threading;

namespace NuGetGallery.DatabaseMigrationTools
{
    class Program
    {
        static int Main(string[] args)
        {
            var migrationContextFactory = new MigrationContextFactory();
            var job = new Job(migrationContextFactory);
            var exitCode = JobRunner.RunOnce(job, args).GetAwaiter().GetResult();

            // Have to use Thread.Sleep() and wait for the logger.
            // Calling "TelemetryChannel.Flush()" on TelemetryConfiguration is not reliable.
            // Hit the issue: https://github.com/Microsoft/ApplicationInsights-dotnet/issues/407
            Thread.Sleep(30000);

            return exitCode;
        }
    }
}
