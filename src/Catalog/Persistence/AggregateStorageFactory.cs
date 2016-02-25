// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AggregateStorageFactory : StorageFactory
    {
        private readonly StorageFactory _primaryStorageFactory;
        private readonly StorageFactory[] _secondaryStorageFactories;

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories)
        {
            _primaryStorageFactory = primaryStorageFactory;
            _secondaryStorageFactories = secondaryStorageFactories;

            BaseAddress = _primaryStorageFactory.BaseAddress;
        }
        
        public override Storage Create(string name = null)
        {
            var primaryStorage = _primaryStorageFactory.Create(name);
            var secondaryStorage = _secondaryStorageFactories.Select(f => f.Create(name)).ToArray();

            return new AggregateStorage(_primaryStorageFactory.BaseAddress, primaryStorage, secondaryStorage);
        }
    }
}