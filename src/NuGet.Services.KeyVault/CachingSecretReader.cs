// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public class CachingSecretReader : ISecretReader
    {
        public const int DefaultRefreshIntervalSec = 60 * 60 * 24; // 1 day
        private readonly int _refreshIntervalSec;

        private readonly ISecretReader _internalReader;
        private readonly Dictionary<string, Tuple<string, DateTime>> _cache;

        public CachingSecretReader(ISecretReader secretReader, int refreshIntervalSec = DefaultRefreshIntervalSec)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            _internalReader = secretReader;
            _cache = new Dictionary<string, Tuple<string, DateTime>>();

            _refreshIntervalSec = refreshIntervalSec;
        }

        public virtual bool IsSecretOutdated(Tuple<string, DateTime> cachedSecret)
        {
            return DateTime.UtcNow.Subtract(cachedSecret.Item2).TotalSeconds >= _refreshIntervalSec;
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            if (!_cache.ContainsKey(secretName) || IsSecretOutdated(_cache[secretName]))
            {
                // Get the secret if it is not yet in the cache or it is outdated.
                var secretValue = await _internalReader.GetSecretAsync(secretName);
                _cache[secretName] = Tuple.Create(secretValue, DateTime.UtcNow);
            }

            return _cache[secretName].Item1;
        }
    }
}