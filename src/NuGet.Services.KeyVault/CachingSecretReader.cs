// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public class CachingSecretReader : ISecretReader
    {
        public const int DefaultRefreshIntervalSec = 60 * 60 * 24; // 1 day
        public const int DefaultRefreshIntervalBeforeExpirySec = 60 * 30 ; // 30 minutes 

        private readonly ISecretReader _internalReader;
        private readonly ConcurrentDictionary<string, CachedSecret> _cache;
        private readonly TimeSpan _refreshInterval;
        //_refreshIntervalBeforeExpiry specifies the timespan before secret expiry to refresh the secret value.
        private readonly TimeSpan _refreshIntervalBeforeExpiry;

        public CachingSecretReader(ISecretReader secretReader, int refreshIntervalSec = DefaultRefreshIntervalSec, int refreshIntervalBeforeExpirySec = DefaultRefreshIntervalBeforeExpirySec)
        {
            _internalReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));
            _cache = new ConcurrentDictionary<string, CachedSecret>();
            _refreshInterval = TimeSpan.FromSeconds(refreshIntervalSec);
            _refreshIntervalBeforeExpiry = TimeSpan.FromSeconds(refreshIntervalBeforeExpirySec);
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            return (await GetSecretObjectAsync(secretName)).Value;            
        }

        public async Task<ISecret> GetSecretObjectAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentException("Null or empty secret name", nameof(secretName));
            }

            // If the cache contains the secret and it is not expired, return the cached value.
            if (_cache.TryGetValue(secretName, out CachedSecret result)
                && !IsSecretOutdated(result))
            {
                return result.Secret;
            }
            // The cache does not contain a fresh copy of the secret. Fetch and cache the secret.
            var updatedValue = new CachedSecret(await _internalReader.GetSecretObjectAsync(secretName));
            return _cache.AddOrUpdate(secretName, updatedValue, (key, old) => updatedValue).Secret;
        }

        private bool IsSecretOutdated(CachedSecret cachedSecret)
        {
            return (((DateTime.UtcNow - cachedSecret.CacheTime) >= _refreshInterval) ||
                (cachedSecret.Secret.Expiration != null &&  (cachedSecret.Secret.Expiration - DateTime.UtcNow) <= _refreshIntervalBeforeExpiry));
        }

        /// <summary>
        /// A cached secret.
        /// </summary>
        private class CachedSecret
        {
            public CachedSecret(ISecret secret)
            {
                Secret = secret;
                CacheTime = DateTimeOffset.UtcNow;
            }
            /// <summary>
            /// The time at which the secret was cached.
            /// </summary>
            public DateTimeOffset CacheTime { get; }
            public ISecret Secret { get; }

        }
    }
}