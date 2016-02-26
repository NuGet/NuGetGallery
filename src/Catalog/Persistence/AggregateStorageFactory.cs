// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AggregateStorageFactory : StorageFactory
    {
        private readonly StorageFactory _primaryStorageFactory;
        private readonly StorageFactory[] _secondaryStorageFactories;
        private readonly AggregateStorage.WriteSecondaryStorageContentInterceptor _writeSecondaryStorageContentInterceptor;

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories)
            : this(primaryStorageFactory, secondaryStorageFactories, null)
        {
        }

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories,
            AggregateStorage.WriteSecondaryStorageContentInterceptor writeSecondaryStorageContentInterceptor)
        { 
            _primaryStorageFactory = primaryStorageFactory;
            _secondaryStorageFactories = secondaryStorageFactories;
            _writeSecondaryStorageContentInterceptor = writeSecondaryStorageContentInterceptor;

            BaseAddress = _primaryStorageFactory.BaseAddress;
        }

        public override Storage Create(string name = null)
        {
            var primaryStorage = _primaryStorageFactory.Create(name);
            var secondaryStorage = _secondaryStorageFactories.Select(f => f.Create(name)).ToArray();

            return new AggregateStorage(_primaryStorageFactory.BaseAddress, primaryStorage, secondaryStorage, _writeSecondaryStorageContentInterceptor);
        }
    }
}