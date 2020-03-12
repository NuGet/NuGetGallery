// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    using Extensions = Microsoft.Extensions.Configuration;

    /// <summary>
    /// Configuration provider that wraps around another provider and does KeyVault secret injection.
    /// </summary>
    /// <remarks>
    /// This relies on configuration objects not to be cached for proper secret rotation from KeyVault.
    /// One needs <see cref="NonCachingOptionsSnapshot{TOptions}"/> as a <see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/> implementation
    /// to make sure no caching happens.
    /// </remarks>
    public class KeyVaultInjectingConfigurationProvider : Extensions.IConfigurationProvider
    {
        private readonly Extensions.IConfigurationProvider _originalProvider;
        private readonly ISecretInjector _secretInjector;
        private static readonly HashSet<string> _notInjectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "GalleryDb:ConnectionString",
            "ValidationDb:ConnectionString",
            "SupportRequestDb:ConnectionString",
            "StatisticsDb:ConnectionString",
            };

        public KeyVaultInjectingConfigurationProvider(Extensions.IConfigurationProvider originalProvider, ISecretInjector secretInjector)
        {
            _originalProvider = originalProvider ?? throw new ArgumentNullException(nameof(originalProvider));
            _secretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
        }

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath)
            => _originalProvider.GetChildKeys(earlierKeys, parentPath);

        public IChangeToken GetReloadToken()
            => _originalProvider.GetReloadToken();

        public void Load()
            => _originalProvider.Load();

        public void Set(string key, string value)
            => _originalProvider.Set(key, value);

        public bool TryGet(string key, out string value)
        {
            if (_originalProvider.TryGet(key, out value))
            {
                if (!_notInjectedKeys.Contains(key))
                {
                    value = _secretInjector.InjectAsync(value).ConfigureAwait(false).GetAwaiter().GetResult();
                }

                return true;
            }

            return false;
        }
    }
}
