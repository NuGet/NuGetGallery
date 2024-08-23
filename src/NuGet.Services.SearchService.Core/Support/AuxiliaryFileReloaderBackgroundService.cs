// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Services.AzureSearch.SearchService;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.SearchService
{
    public class AuxiliaryFileReloaderBackgroundService : BackgroundService
    {
        private readonly IAuxiliaryFileReloader _reloader;

        public AuxiliaryFileReloaderBackgroundService(IAuxiliaryFileReloader reloader)
        {
            _reloader = reloader ?? throw new ArgumentNullException(nameof(reloader));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _reloader.ReloadContinuouslyAsync(stoppingToken);
        }
    }
}
