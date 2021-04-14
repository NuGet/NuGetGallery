// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    using Extensions = Microsoft.Extensions.Configuration;

    public class KeyVaultInMemoryCollectionInjectingConfigurationSource : IConfigurationSource 
    {
        private readonly IReadOnlyDictionary<string, string> _dictionary;
        private readonly ISecretInjector _secretInjector;

        public KeyVaultInMemoryCollectionInjectingConfigurationSource(IReadOnlyDictionary<string, string> dictionary, ISecretInjector secretInjector)
        {
            _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            _secretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
        }

        public Extensions.IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var source = new MemoryConfigurationSource()
            {
                InitialData = _dictionary
                    .Select(x => new KeyValuePair<string, string>(x.Key, x.Value))
            };

            var provider = source.Build(builder);
            return new KeyVaultInjectingConfigurationProvider(provider, _secretInjector);
        }
    }
}
