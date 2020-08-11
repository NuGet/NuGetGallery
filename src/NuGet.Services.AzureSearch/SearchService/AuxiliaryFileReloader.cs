// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryFileReloader : IAuxiliaryFileReloader
    {
        private readonly IAuxiliaryDataCache _cache;
        private readonly ISystemTime _systemTime;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly ILogger<AuxiliaryFileReloader> _logger;

        public AuxiliaryFileReloader(
            IAuxiliaryDataCache cache,
            ISystemTime systemTime,
            IOptionsSnapshot<SearchServiceConfiguration> options,
            ILogger<AuxiliaryFileReloader> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _systemTime = systemTime ?? throw new ArgumentNullException(nameof(systemTime));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ReloadContinuouslyAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _logger.LogInformation("Trying to reload the auxiliary data.");

                TimeSpan delay;
                try
                {
                    await _cache.TryLoadAsync(token);
                    delay = _options.Value.AuxiliaryDataReloadFrequency;
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "An exception was thrown while reloading the auxiliary data.");

                    delay = _options.Value.AuxiliaryDataReloadFailureRetryFrequency;
                }
                
                if (token.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogInformation(
                    "Waiting {Duration} before attempting to reload the auxiliary data again.",
                    delay);
                await _systemTime.Delay(delay, token);
            }
        }
    }
}
