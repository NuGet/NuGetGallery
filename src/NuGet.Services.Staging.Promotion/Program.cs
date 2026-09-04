// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Jobs;

namespace NuGet.Services.Staging.Promotion
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await JobRunner.RunOnce(new Job(), args);
        }
    }
}
