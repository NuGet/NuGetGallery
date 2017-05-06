// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
{
    public class AggregateStorageFactory : StorageFactory
    {
        private readonly AggregateStorage.WriteSecondaryStorageContentInterceptor _writeSecondaryStorageContentInterceptor;
        private readonly ILogger<AggregateStorage> _aggregateStorageLogger;

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, ICollection<StorageFactory> secondaryStorageFactories, ILogger<AggregateStorage> aggregateStorageLogger)
            : this(primaryStorageFactory, secondaryStorageFactories, null, aggregateStorageLogger)
        {
        }

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, ICollection<StorageFactory> secondaryStorageFactories,
            AggregateStorage.WriteSecondaryStorageContentInterceptor writeSecondaryStorageContentInterceptor,
            ILogger<AggregateStorage> aggregateStorageLogger)
        { 
            PrimaryStorageFactory = primaryStorageFactory;
            SecondaryStorageFactories = secondaryStorageFactories;
            _writeSecondaryStorageContentInterceptor = writeSecondaryStorageContentInterceptor;
            _aggregateStorageLogger = aggregateStorageLogger;

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
                _writeSecondaryStorageContentInterceptor,
                _aggregateStorageLogger);
        }

        public StorageFactory PrimaryStorageFactory { get; }
        public IEnumerable<StorageFactory> SecondaryStorageFactories { get; }
    }
}