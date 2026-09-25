// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Jobs;

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var job = new AggregateCdnDownloadsJob();
            await JobRunner.Run(job, args);
        }
    }
}
