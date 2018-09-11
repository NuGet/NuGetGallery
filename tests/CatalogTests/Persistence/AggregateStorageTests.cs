// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Persistence
{
    public class AggregateStorageTests
    {
        private readonly AggregateStorage _storage;

        public AggregateStorageTests()
        {
            _storage = CreateNewAggregateStorage();
        }

        [Fact]
        public async Task CopyAsync_Always_Throws()
        {
            var destinationStorage = CreateNewAggregateStorage();
            var sourceFileUri = _storage.ResolveUri("a");
            var destinationFileUri = destinationStorage.ResolveUri("a");

            await Assert.ThrowsAsync<NotImplementedException>(
                () => _storage.CopyAsync(
                    sourceFileUri,
                    destinationStorage,
                    destinationFileUri,
                    destinationProperties: null,
                    cancellationToken: CancellationToken.None));
        }

        private static AggregateStorage CreateNewAggregateStorage()
        {
            return new AggregateStorage(
                new Uri("https://nuget.test"),
                new MemoryStorage(),
                secondaryStorage: null,
                writeSecondaryStorageContentInterceptor: null);
        }
    }
}