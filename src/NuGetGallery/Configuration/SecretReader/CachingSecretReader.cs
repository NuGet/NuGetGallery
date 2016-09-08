﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Configuration.SecretReader
{
    public class CachingSecretReader : ISecretReader
    {
        private readonly int _refreshIntervalSeconds;

        private ISecretReader _internalReader;
        private Dictionary<string, Tuple<string, DateTime>> _cache;
        private IDiagnosticsSource _trace;

        public CachingSecretReader(ISecretReader secretReader, IDiagnosticsService diagnosticsService, int refreshInterval)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }

            _internalReader = secretReader;
            _cache = new Dictionary<string, Tuple<string, DateTime>>();
            _trace = diagnosticsService.GetSource("CachingSecretReader");

            _refreshIntervalSeconds = refreshInterval;
        }
            
        public async Task<string> GetSecretAsync(string secretName)
        {
            if (_cache.ContainsKey(secretName))
            {
                if (DateTime.UtcNow.Subtract(_cache[secretName].Item2).TotalSeconds < _refreshIntervalSeconds)
                {
                    // If the secret is in the cache and does not need to be refreshed, return from the cache
                    return _cache[secretName].Item1;
                } else
                {
                    _trace.Information("Must refresh setting " + secretName);
                }
            } else
            {
                _trace.Information("Cache miss for setting " + secretName);
            }

            // If the secret is not in the cache or needs to be refreshed, refresh from KeyVault
            var secretValue = await _internalReader.GetSecretAsync(secretName);
            _cache[secretName] = Tuple.Create<string, DateTime>(secretValue, DateTime.UtcNow);
            return _cache[secretName].Item1;
        }
    }
}