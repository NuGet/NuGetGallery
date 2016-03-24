// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;

namespace Search.GenerateAuxiliaryData
{
    public class Program
    {
        public static void Main(string[] args)
        {
            JobRunner.Run(new Job(), args).Wait();
        }
    }
}
