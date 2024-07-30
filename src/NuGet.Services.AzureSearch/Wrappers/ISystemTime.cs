// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.Wrappers
{
    /// <summary>
    /// A wrapper that allows for unit tests related to system time.
    /// </summary>
    public interface ISystemTime
    {
        Task Delay(TimeSpan delay);
        Task Delay(TimeSpan delay, CancellationToken token);
    }
}