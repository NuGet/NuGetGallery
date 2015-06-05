// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Search.Client
{
    public class NullHealthIndicatorLogger
        : IHealthIndicatorLogger
    {
        public void LogDecreaseHealth(Uri endpoint, int health, Exception exception)
        {
            // noop
        }

        public void LogIncreaseHealth(Uri endpoint, int health)
        {
            // noop
        }
    }
}