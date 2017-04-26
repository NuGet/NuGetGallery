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
        private readonly ILoggerFactory _loggerFactory;

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories, ILoggerFactory loggerFactory)
            : this(primaryStorageFactory, secondaryStorageFactories, null, loggerFactory)
        {
        }

        public AggregateStorageFactory(StorageFactory primaryStorageFactory, StorageFactory[] secondaryStorageFactories,
            AggregateStorage.WriteSecondaryStorageContentInterceptor writeSecondaryStorageContentInterceptor,
            ILoggerFactory loggerFactory)
        { 
            PrimaryStorageFactory = primaryStorageFactory;
            SecondaryStorageFactories = secondaryStorageFactories;
            _writeSecondaryStorageContentInterceptor = writeSecondaryStorageContentInterceptor;
            _loggerFactory = loggerFactory;

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
                _loggerFactory);
        }

        public StorageFactory PrimaryStorageFactory { get; }
        public IEnumerable<StorageFactory> SecondaryStorageFactories { get; }
    }
}