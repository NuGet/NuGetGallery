// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;

namespace NuGet.Services.Validation.Orchestrator
{
    class Program
    {
        static int Main(string[] args)
        {
            var job = new Job();
            JobRunner.RunOnce(job, args).GetAwaiter().GetResult();

            // if configuration validation failed, return non-zero status so we can detect failures in automation
            return job.ConfigurationValidated ? 0 : 1;
        }
    }
}
