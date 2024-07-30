// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public class TestStorageFactory 
        : StorageFactory
    {
        private readonly Func<string, Storage> _createStorage;

        public TestStorageFactory()
            : this(name => new MemoryStorage())
        {
        }

        public TestStorageFactory(Func<string, Storage> createStorage)
        {
            _createStorage = createStorage;

            BaseAddress = _createStorage(null).BaseAddress;
        }

        public override Storage Create(string name = null)
        {
            return _createStorage(name);
        }
    }
}