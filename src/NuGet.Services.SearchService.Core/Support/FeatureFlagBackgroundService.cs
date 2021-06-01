// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NuGet.Services.FeatureFlags;

namespace NuGet.Services.SearchService
{
    public class FeatureFlagBackgroundService : BackgroundService
    {
        private readonly IFeatureFlagCacheService _flagsCache;

        public FeatureFlagBackgroundService(IFeatureFlagCacheService flagsCache)
        {
            _flagsCache = flagsCache ?? throw new ArgumentNullException(nameof(flagsCache));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _flagsCache.RunAsync(stoppingToken);
        }
    }
}
