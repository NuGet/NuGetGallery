﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;

namespace GitHubVulnerabilities2Db
{
    class Program
    {
        public static void Main(string[] args)
        {
            var job = new Job();
            JobRunner.Run(job, args).Wait();
        }
    }
}