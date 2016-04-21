// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace NuGet.Indexing
{
    public static class Retry
    {
        public static void Incremental(Action runLogic, Func<Exception, bool> shouldRetry, int maxRetries, TimeSpan waitIncrement)
        {
            for (int currentRetry = 0; currentRetry < maxRetries; currentRetry++)
            {
                try
                {
                    runLogic();
                    return;
                }
                catch (Exception e)
                {
                    if (currentRetry < maxRetries && shouldRetry(e))
                    {
                        Thread.Sleep((currentRetry + 1) * (int)waitIncrement.TotalMilliseconds);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}