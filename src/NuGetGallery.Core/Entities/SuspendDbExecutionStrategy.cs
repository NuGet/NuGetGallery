// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Define the execution strategy for the EntitiesConfiguration for connection resiliency and retries
    /// </summary>
    public class SuspendDbExecutionStrategy : IDisposable
    {
        public SuspendDbExecutionStrategy()
        {
            EntitiesConfiguration.SuspendExecutionStrategy = true;
        }

        public void Dispose()
        {
            EntitiesConfiguration.SuspendExecutionStrategy = false;
        }
    }
}
