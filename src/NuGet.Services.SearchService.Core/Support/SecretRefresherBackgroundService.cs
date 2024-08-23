// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Services.AzureSearch.SearchService;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.SearchService
{
    public class SecretRefresherBackgroundService : BackgroundService
    {
        private readonly ISecretRefresher _refresher;

        public SecretRefresherBackgroundService(ISecretRefresher refresher)
        {
            _refresher = refresher ?? throw new ArgumentNullException(nameof(refresher));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _refresher.RefreshContinuouslyAsync(stoppingToken);
        }
    }
}
