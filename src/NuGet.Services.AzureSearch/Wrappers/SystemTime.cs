// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SystemTime : ISystemTime
    {
        public async Task Delay(TimeSpan delay)
        {
            await Task.Delay(delay);
        }

        public async Task Delay(TimeSpan delay, CancellationToken token)
        {
            await Task.Delay(delay, token);
        }
    }
}
