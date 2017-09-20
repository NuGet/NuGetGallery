// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Services.Configuration;

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
