// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AggregateStorageFactory : StorageFactory
    {
        private readonly AggregateStorage.WriteSecondaryStorageContentInterceptor _writeSecondaryStorageContentInterceptor;

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories)
            : this(primaryStorageFactory, secondaryStorageFactories, null)
        {
        }

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories,
            AggregateStorage.WriteSecondaryStorageContentInterceptor writeSecondaryStorageContentInterceptor)
        { 
            PrimaryStorageFactory = primaryStorageFactory;
            SecondaryStorageFactories = secondaryStorageFactories;
            _writeSecondaryStorageContentInterceptor = writeSecondaryStorageContentInterceptor;

            BaseAddress = PrimaryStorageFactory.BaseAddress;
        }

        public override Storage Create(string name = null)
        {
            var primaryStorage = PrimaryStorageFactory.Create(name);
            var secondaryStorage = SecondaryStorageFactories.Select(f => f.Create(name)).ToArray();

            return new AggregateStorage(
                PrimaryStorageFactory.BaseAddress,
                primaryStorage,
                secondaryStorage,
                _writeSecondaryStorageContentInterceptor);
        }

        public StorageFactory PrimaryStorageFactory { get; }
        public IEnumerable<StorageFactory> SecondaryStorageFactories { get; }
    }
}