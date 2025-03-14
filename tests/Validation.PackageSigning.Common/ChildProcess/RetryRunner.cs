// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit.Abstractions;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess
{
    public class RetryRunner
    {
        public static T RunWithRetries<T, E>(Func<T> func, int maxRetries = 1, ITestOutputHelper logger = null) where E : Exception
        {
            {
                int retryCount = 0;

                while (true)
                {
                    try
                    {
                        return func();
                    }
                    catch (E exception)
                    {
                        if (retryCount >= maxRetries)
                        {
                            throw;
                        }

                        retryCount++;
                        logger?.WriteLine($"Encountered exception during run attempt #{retryCount}: {exception.Message}");
                        logger?.WriteLine($"Retrying {retryCount} of {maxRetries}");
                    }
                }
            }
        }
    }
}
