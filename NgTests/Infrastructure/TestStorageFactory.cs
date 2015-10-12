// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public class TestStorageFactory 
        : StorageFactory
    {
        private readonly Func<Storage> _createStorage;

        public TestStorageFactory()
            : this(() => new MemoryStorage())
        {
        }

        public TestStorageFactory(Func<Storage> createStorage)
        {
            _createStorage = createStorage;
        }

        public override Storage Create(string name = null)
        {
            return _createStorage();
        }
    }
}