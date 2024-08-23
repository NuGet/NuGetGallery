// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var job = new Job();
            return JobRunner.RunOnce(job, args).GetAwaiter().GetResult();
        }
    }
}
