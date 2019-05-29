// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace NuGet.Jobs
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var job = new Job();
            var exitCode = JobRunner.Run(job, args).GetAwaiter().GetResult();

            // Sleep to allow Application Insights to flush all logs. See issue:
            // https://github.com/Microsoft/ApplicationInsights-dotnet/issues/407
            Thread.Sleep(30000);

            return exitCode;
        }
    }
}
