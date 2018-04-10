// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Search.Client
{
    /// <summary>
    /// A thread safe random implementation 
    /// https://blogs.msdn.microsoft.com/pfxteam/2009/02/19/getting-random-numbers-in-a-thread-safe-way/
    /// </summary>
    public static class ThreadSafeRandom
    {
        private static Random _global = new Random();

        [ThreadStatic]
        private static Random _local;

        public static int Next()
        {
            Random inst = GetLocalInstance();
            return inst.Next();
        }

        public static int Next(int min, int max)
        {
            Random inst = GetLocalInstance();
            return inst.Next(min, max);
        }

        static Random GetLocalInstance()
        {
            Random inst = _local;
            if (inst == null)
            {
                int seed;
                lock (_global)
                {
                    seed = _global.Next();
                }
                _local = inst = new Random(seed);
            }
            return inst;
        }
    }
}
