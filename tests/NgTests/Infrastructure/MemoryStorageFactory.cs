// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public class MemoryStorageFactory : StorageFactory
    {
        private Dictionary<string, MemoryStorage> _cachedStorages;

        public MemoryStorageFactory()
        {
            _cachedStorages = new Dictionary<string, MemoryStorage>();
        }

        public override Storage Create(string name = null)
        {
            if (!_cachedStorages.ContainsKey(name))
            {
                _cachedStorages[name] = (MemoryStorage)new MemoryStorage().WithName(name);
            }

            return _cachedStorages[name];
        }
    }
}
