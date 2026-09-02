// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;

namespace NuGet.Services.Staging.Promotion
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            JobRunner.RunOnce(new Job(), args).GetAwaiter().GetResult();
        }
    }
}
