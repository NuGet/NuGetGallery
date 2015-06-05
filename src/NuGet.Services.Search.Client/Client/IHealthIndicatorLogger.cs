// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Search.Client
{
    public interface IHealthIndicatorLogger
    {
        void LogDecreaseHealth(Uri endpoint, int health, Exception exception);
        void LogIncreaseHealth(Uri endpoint, int health);
    }
}