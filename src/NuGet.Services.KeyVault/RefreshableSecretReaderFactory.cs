// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// Wraps existing secret reader factory to provide a caching layer where the cache refresh is controlled by
    /// the <see cref="RefreshAsync"/> method on the factory created <see cref="ISecretReader"/> instances.
    /// </summary>
    public class RefreshableSecretReaderFactory : IRefreshableSecretReaderFactory
    {
        private readonly ISecretReaderFactory _underlyingFactory;
        private readonly ConcurrentDictionary<string, ISecret> _cache;
        private readonly RefreshableSecretReaderSettings _settings;

        public RefreshableSecretReaderFactory(ISecretReaderFactory underlyingFactory, RefreshableSecretReaderSettings settings)
        {
            _underlyingFactory = underlyingFactory ?? throw new ArgumentNullException(nameof(underlyingFactory));
            _cache = new ConcurrentDictionary<string, ISecret>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task RefreshAsync(CancellationToken token)
        {
            await GetRefreshableSecretReader().RefreshAsync(token);
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return _underlyingFactory.CreateSecretInjector(secretReader);
        }

        public ISecretReader CreateSecretReader()
        {
            return GetRefreshableSecretReader();
        }

        private RefreshableSecretReader GetRefreshableSecretReader()
        {
            var innerSecretReader = _underlyingFactory.CreateSecretReader();
            return new RefreshableSecretReader(innerSecretReader, _cache, _settings);
        }
    }
}
