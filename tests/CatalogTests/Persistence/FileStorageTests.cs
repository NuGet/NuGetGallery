// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Persistence
{
    public class FileStorageTests
    {
        private readonly FileStorage _storage;

        public FileStorageTests()
        {
            _storage = CreateNewFileStorage();
        }

        [Fact]
        public async Task CopyAsync_Always_Throws()
        {
            var destinationStorage = CreateNewFileStorage();
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

        private static FileStorage CreateNewFileStorage()
        {
            return new FileStorage(Path.GetTempPath(), Guid.NewGuid().ToString("N"), verbose: false);
        }
    }
}