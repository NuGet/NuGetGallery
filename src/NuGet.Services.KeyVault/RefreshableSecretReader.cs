// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// A secret reader that separates the refreshing of secret values from the reading of them from an in-memory
    /// cache. Although the <see cref="GetSecretAsync(string)"/> and <see cref="GetSecretObjectAsync(string)"/> methods
    /// are asynchronous in definition they do not result an any asynchronous operations when a secret has already been
    /// cached with a previous invocation. The <see cref="RefreshAsync"/> method is used to refresh the values of
    /// secrets that have already been cached.
    /// </summary>
    public class RefreshableSecretReader : ISecretReader
    {
        private readonly ISecretReader _secretReader;
        private readonly ConcurrentDictionary<string, ISecret> _cache;
        private readonly RefreshableSecretReaderSettings _settings;

        public RefreshableSecretReader(
            ISecretReader secretReader,
            ConcurrentDictionary<string, ISecret> cache,
            RefreshableSecretReaderSettings settings)
        {
            _secretReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task RefreshAsync(CancellationToken token)
        {
            foreach (var secretName in _cache.Keys)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await UncachedGetSecretObjectAsync(secretName);
            }
        }

        public Task<string> GetSecretAsync(string secretName)
        {
            if (TryGetCachedSecretObject(secretName, out var secret))
            {
                return Task.FromResult(secret.Value);
            }

            return UncachedGetSecretAsync(secretName);
        }

        public Task<ISecret> GetSecretObjectAsync(string secretName)
        {
            if (TryGetCachedSecretObject(secretName, out var secret))
            {
                return Task.FromResult(secret);
            }

            return UncachedGetSecretObjectAsync(secretName);
        }

        private async Task<string> UncachedGetSecretAsync(string secretName)
        {
            var secretObject = await UncachedGetSecretObjectAsync(secretName);
            return secretObject.Value;
        }

        private async Task<ISecret> UncachedGetSecretObjectAsync(string secretName)
        {
            var secretObject = await _secretReader.GetSecretObjectAsync(secretName);
            _cache.AddOrUpdate(secretName, secretObject, (_, __) => secretObject);
            return secretObject;
        }

        private bool TryGetCachedSecretObject(string secretName, out ISecret secret)
        {
            if (_cache.TryGetValue(secretName, out secret))
            {
                return true;
            }

            if (_settings.BlockUncachedReads)
            {
                throw new InvalidOperationException($"The secret '{secretName}' is not cached.");
            }

            secret = null;
            return false;
        }
    }
}