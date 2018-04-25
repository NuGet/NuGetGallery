// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public class CachingSecretReader : ICachingSecretReader
    {
        public const int DefaultRefreshIntervalSec = 60 * 60 * 24; // 1 day

        private readonly ISecretReader _internalReader;
        private readonly ConcurrentDictionary<string, CachedSecret> _cache;
        private readonly ConcurrentDictionary<string, CachedCertificateSecret> _certificateCache;
        private readonly TimeSpan _refreshInterval;

        public CachingSecretReader(ISecretReader secretReader, int refreshIntervalSec = DefaultRefreshIntervalSec)
        {
            _internalReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));
            _cache = new ConcurrentDictionary<string, CachedSecret>();
            _certificateCache = new ConcurrentDictionary<string, CachedCertificateSecret>();

            _refreshInterval = TimeSpan.FromSeconds(refreshIntervalSec);
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw Exceptions.ArgumentNullOrEmpty(nameof(secretName));
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

        public async Task<X509Certificate2> GetCertificateSecretAsync(string secretName, string password)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw Exceptions.ArgumentNullOrEmpty(nameof(secretName));
            }

            if (password == null)
            {
                throw new ArgumentNullException(nameof(secretName));
            }

            // If the cache contains the secret and it is not expired, return the cached value.
            if (_certificateCache.TryGetValue(secretName, out CachedCertificateSecret result)
                && !IsSecretOutdated(result))
            {
                return result.Value;
            }

            // The cache does not contain a fresh copy of the secret. Fetch and cache the secret.
            var updatedValue = new CachedCertificateSecret(await _internalReader.GetCertificateSecretAsync(secretName, password));

            return _certificateCache.AddOrUpdate(secretName, updatedValue, (key, old) => updatedValue)
                         .Value;
        }

        public bool RefreshSecret(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw Exceptions.ArgumentNullOrEmpty(nameof(secretName));
            }

            return _cache.TryRemove(secretName, out CachedSecret value);
        }

        public bool RefreshCertificateSecret(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw Exceptions.ArgumentNullOrEmpty(nameof(secretName));
            }

            return _certificateCache.TryRemove(secretName, out CachedCertificateSecret value);
        }

        protected virtual bool IsSecretOutdated<T>(ICachedSecret<T> secret)
        {
            return (DateTime.UtcNow - secret.CacheTime) >= _refreshInterval;
        }

        public interface ICachedSecret<T>
        {
            T Value { get; }

            DateTimeOffset CacheTime { get; }
        }

        /// <summary>
        /// A cached secret.
        /// </summary>
        private class CachedSecret : ICachedSecret<string>
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

        /// <summary>
        /// A cached certificate secret.
        /// </summary>
        private class CachedCertificateSecret : ICachedSecret<X509Certificate2>
        {
            public CachedCertificateSecret(X509Certificate2 value)
            {
                Value = value;
                CacheTime = DateTimeOffset.UtcNow;
            }

            /// <summary>
            /// The value of the cached certificate secret.
            /// </summary>
            public X509Certificate2 Value { get; }

            /// <summary>
            /// The time at which the certificate secret was cached.
            /// </summary>
            public DateTimeOffset CacheTime { get; }
        }
    }
}