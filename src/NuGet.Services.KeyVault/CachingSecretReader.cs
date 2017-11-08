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

        private readonly ISecretReader _internalReader;
        private readonly ConcurrentDictionary<string, CachedSecret> _cache;
        private readonly TimeSpan _refreshInterval;

        public CachingSecretReader(ISecretReader secretReader, int refreshIntervalSec = DefaultRefreshIntervalSec)
        {
            _internalReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));
            _cache = new ConcurrentDictionary<string, CachedSecret>();

            _refreshInterval = TimeSpan.FromSeconds(refreshIntervalSec);
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentException("Null or empty secret name", nameof(secretName));
            }

            // If the cache contains the secret and it is not expired, return the cached value.
            if (_cache.TryGetValue(secretName, out CachedSecret result)
                && !IsSecretOutdated(result))
            {
                return result.Value;
            }

            // The cache does not contain a fresh copy of the secret. Fetch and cache the secret.
            var updatedValue = new CachedSecret(await _internalReader.GetSecretAsync(secretName));

            return _cache.AddOrUpdate(secretName, updatedValue, (key, old) => updatedValue)
                         .Value;
        }

        private bool IsSecretOutdated(CachedSecret secret)
        {
            return (DateTime.UtcNow - secret.CacheTime) >= _refreshInterval;
        }

        /// <summary>
        /// A cached secret.
        /// </summary>
        private class CachedSecret
        {
            public CachedSecret(string value)
            {
                Value = value;
                CacheTime = DateTimeOffset.UtcNow;
            }

            /// <summary>
            /// The value of the cached secret.
            /// </summary>
            public string Value { get; }

            /// <summary>
            /// The time at which the secret was cached.
            /// </summary>
            public DateTimeOffset CacheTime { get; }
        }
    }
}