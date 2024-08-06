﻿// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.KeyVault
{
    public class CachingSecretReader : ICachingSecretReader
    {
        public const int DefaultRefreshIntervalSec = 60 * 60 * 24; // 1 day
        public const int DefaultRefreshIntervalBeforeExpirySec = 60 * 30 ; // 30 minutes

        private readonly ISecretReader _internalReader;
        private readonly ConcurrentDictionary<string, CachedSecret> _cache;
        private readonly TimeSpan _refreshInterval;
        //_refreshIntervalBeforeExpiry specifies the timespan before secret expiry to refresh the secret value.
        private readonly TimeSpan _refreshIntervalBeforeExpiry;

        public CachingSecretReader(ISecretReader secretReader,
            int refreshIntervalSec = DefaultRefreshIntervalSec,
            int refreshIntervalBeforeExpirySec = DefaultRefreshIntervalBeforeExpirySec)
        {
            _internalReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));
            _cache = new ConcurrentDictionary<string, CachedSecret>();
            _refreshInterval = TimeSpan.FromSeconds(refreshIntervalSec);
            _refreshIntervalBeforeExpiry = TimeSpan.FromSeconds(refreshIntervalBeforeExpirySec);
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            return await GetSecretAsync(secretName, logger: null);
        }

        public async Task<string> GetSecretAsync(string secretName, ILogger logger)
        {
            return (await GetSecretObjectAsync(secretName, logger)).Value;
        }

        public async Task<ISecret> GetSecretObjectAsync(string secretName)
        {
            return await GetSecretObjectAsync(secretName, logger: null);
        }

        public async Task<ISecret> GetSecretObjectAsync(string secretName, ILogger logger)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentException("Null or empty secret name", nameof(secretName));
            }

            // If the cache contains the secret and it is not expired, return the cached value.
            if (TryGetCachedSecretObject(secretName, logger, out var cachedSecret))
            {
                return cachedSecret;
            }

            var start = DateTimeOffset.UtcNow;
            // The cache does not contain a fresh copy of the secret. Fetch and cache the secret.
            var updatedValue = new CachedSecret(await _internalReader.GetSecretObjectAsync(secretName));
            var updatedSecret = _cache.AddOrUpdate(secretName, updatedValue, (key, old) => updatedValue).Secret;

            logger?.LogInformation("Refreshed secret {SecretName}, Expiring at: {ExpirationTime}. Took {ElapsedMilliseconds}ms.",
                updatedSecret.Name,
                updatedSecret.Expiration == null ? "null" : ((DateTimeOffset) updatedSecret.Expiration).UtcDateTime.ToString(),
                (DateTimeOffset.UtcNow - start).TotalMilliseconds.ToString("F2"));

            return updatedSecret;
        }

        public bool TryGetCachedSecret(string secretName, out string secretValue) => TryGetCachedSecret(secretName, logger: null, out secretValue);

        public bool TryGetCachedSecret(string secretName, ILogger logger, out string secretValue)
        {
            secretValue = null;
            if (TryGetCachedSecretObject(secretName, logger, out var secretObject))
            {
                secretValue = secretObject.Value;
                return true;
            }
            return false;
        }

        public bool TryGetCachedSecretObject(string secretName, out ISecret secretObject) => TryGetCachedSecretObject(secretName, logger: null, out secretObject);

        public bool TryGetCachedSecretObject(string secretName, ILogger logger, out ISecret secretObject)
        {
            secretObject = null;
            if (_cache.TryGetValue(secretName, out CachedSecret result)
                && !IsSecretOutdated(result))
            {
                secretObject = result.Secret;
                return true;
            }

            return false;
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