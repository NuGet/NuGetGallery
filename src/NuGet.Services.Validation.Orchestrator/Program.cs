// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Jobs;

namespace NuGet.Services.Validation.Orchestrator
{
    class Program
    {
        private const string LoggingCategory = "Validation.Orchestrator";

        static int Main(string[] args)
        {
            if (!args.Contains(JobArgumentNames.Once))
            {
                args = args.Concat(new[] { "-" + JobArgumentNames.Once }).ToArray();
            }
            var job = new Job();
            JobRunner.Run(job, args).Wait();

            // if configuration validation failed, return non-zero status so we can detect failures in automation
            return job.ConfigurationValidated ? 0 : 1;
        }
    }
}
