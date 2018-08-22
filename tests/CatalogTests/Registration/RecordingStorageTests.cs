// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace CatalogTests.Registration
{
    public class RecordingStorageTests
    {
        private readonly RecordingStorage _storage;

        public RecordingStorageTests()
        {
            _storage = CreateNewRecordingStorage();
        }

        [Fact]
        public async Task CopyAsync_Always_Throws()
        {
            var destinationStorage = CreateNewRecordingStorage();
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

        private static RecordingStorage CreateNewRecordingStorage()
        {
            return new RecordingStorage(new MemoryStorage());
        }
    }
}