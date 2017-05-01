// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation.Helper
{
    class Program
    {
        private static void Main(string[] args)
        {
            if (args.Count() < 1)
            {
                Job.PrintUsage();
                return;
            }

            // force running once
            if (!args.Contains("-Once"))
            {
                args = args.Concat(new[] { "-Once" }).ToArray();
            }
            // Disable logging to azure
            if (!args.Contains("-ConsoleLogOnly"))
            {
                args = new[] { "-ConsoleLogOnly" }.Concat(args).ToArray();
            }

            var job = new Job();
            JobRunner.Run(job, args).Wait();
        }
    }
}
