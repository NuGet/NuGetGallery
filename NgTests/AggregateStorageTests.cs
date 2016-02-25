// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests
{
    public class AggregateStorageTests
    {
        [Fact]
        public async Task PropagatesSaveToAllStorage()
        {
            // Arrange
            var storage1 = new MemoryStorage(new Uri("http://tempuri.org/firstone"));
            var storage2 = new MemoryStorage(new Uri("http://tempuri.org/secondone"));
            var storage3 = new MemoryStorage(new Uri("http://tempuri.org/thirdone"));

            var aggregateStorageFactory = Create(storage1, storage2, storage3);
            var aggregateStorageRoot = aggregateStorageFactory.Create();
            var aggregateStorageSub = aggregateStorageFactory.Create("sub");

            // Act
            await aggregateStorageRoot.Save(new Uri("http://tempuri.org/firstone/test.txt"), new StringStorageContent("test1"), CancellationToken.None);
            await aggregateStorageSub.Save(new Uri("http://tempuri.org/firstone/sub/test.txt"), new StringStorageContent("test2"), CancellationToken.None);

            // Assert
            Assert.Equal(2, storage1.Content.Count);
            Assert.Equal(2, storage2.Content.Count);
            Assert.Equal(2, storage3.Content.Count);

            AssertUriAndContentExists(storage1, new Uri("http://tempuri.org/firstone/test.txt"), "test1");
            AssertUriAndContentExists(storage2, new Uri("http://tempuri.org/secondone/test.txt"), "test1");
            AssertUriAndContentExists(storage3, new Uri("http://tempuri.org/thirdone/test.txt"), "test1");

            AssertUriAndContentExists(storage1, new Uri("http://tempuri.org/firstone/sub/test.txt"), "test2");
            AssertUriAndContentExists(storage2, new Uri("http://tempuri.org/secondone/sub/test.txt"), "test2");
            AssertUriAndContentExists(storage3, new Uri("http://tempuri.org/thirdone/sub/test.txt"), "test2");
        }

        [Fact]
        public async Task PropagatesDeleteToAllStorage()
        {
            // Arrange
            var storage1 = new MemoryStorage(new Uri("http://tempuri.org/firstone"));
            var storage2 = new MemoryStorage(new Uri("http://tempuri.org/secondone"));
            var storage3 = new MemoryStorage(new Uri("http://tempuri.org/thirdone"));

            var aggregateStorageFactory = Create(storage1, storage2, storage3);
            var aggregateStorageRoot = aggregateStorageFactory.Create();
            var aggregateStorageSub = aggregateStorageFactory.Create("sub");

            await aggregateStorageRoot.Save(new Uri("http://tempuri.org/firstone/test.txt"), new StringStorageContent("test1"), CancellationToken.None);
            await aggregateStorageSub.Save(new Uri("http://tempuri.org/firstone/sub/test.txt"), new StringStorageContent("test2"), CancellationToken.None);

            // Act
            await aggregateStorageRoot.Delete(new Uri("http://tempuri.org/firstone/test.txt"), CancellationToken.None);

            // Assert
            Assert.Equal(1, storage1.Content.Count);
            Assert.Equal(1, storage2.Content.Count);
            Assert.Equal(1, storage3.Content.Count);

            AssertUriAndContentDoesNotExist(storage1, new Uri("http://tempuri.org/firstone/test.txt"), "test1");
            AssertUriAndContentDoesNotExist(storage2, new Uri("http://tempuri.org/secondone/test.txt"), "test1");
            AssertUriAndContentDoesNotExist(storage3, new Uri("http://tempuri.org/thirdone/test.txt"), "test1");

            AssertUriAndContentExists(storage1, new Uri("http://tempuri.org/firstone/sub/test.txt"), "test2");
            AssertUriAndContentExists(storage2, new Uri("http://tempuri.org/secondone/sub/test.txt"), "test2");
            AssertUriAndContentExists(storage3, new Uri("http://tempuri.org/thirdone/sub/test.txt"), "test2");
        }

        [Fact]
        public async Task AlwaysReadsFromPrimary()
        {
            // Arrange
            var storage1 = new MemoryStorage(new Uri("http://tempuri.org/firstone"));
            var storage2 = new MemoryStorage(new Uri("http://tempuri.org/secondone"));
            var storage3 = new MemoryStorage(new Uri("http://tempuri.org/thirdone"));

            var aggregateStorageFactory = Create(storage1, storage2, storage3);
            var aggregateStorage = aggregateStorageFactory.Create();

            await aggregateStorage.Save(new Uri("http://tempuri.org/firstone/test.txt"), new StringStorageContent("test1"), CancellationToken.None);

            // Act
            var content1 = await aggregateStorage.Load(new Uri("http://tempuri.org/firstone/test.txt"), CancellationToken.None);
            var content2 = await aggregateStorage.Load(new Uri("http://tempuri.org/secondone/test.txt"), CancellationToken.None);
            var content3 = await aggregateStorage.Load(new Uri("http://tempuri.org/thirdone/test.txt"), CancellationToken.None);

            // Assert
            Assert.NotNull(content1);
            Assert.Null(content2);
            Assert.Null(content3);
        }

        protected AggregateStorageFactory Create(MemoryStorage primaryStorage, MemoryStorage secondaryStorage, params MemoryStorage[] storages)
        {
            var storageFactories = new List<StorageFactory>();
            storageFactories.Add(new TestStorageFactory(name => primaryStorage.WithName(name)));
            storageFactories.Add(new TestStorageFactory(name => secondaryStorage.WithName(name)));

            foreach (var storage in storages)
            {
                storageFactories.Add(new TestStorageFactory(name => storage.WithName(name)));
            }

            return new AggregateStorageFactory(storageFactories.First(), storageFactories.Skip(1).ToArray());
        }

        protected void AssertUriAndContentExists(MemoryStorage storage, Uri uri, string expectedContent)
        {
            var value = storage.Content.FirstOrDefault(pair => pair.Key == uri);

            Assert.NotNull(value.Key);
            Assert.Equal(expectedContent, value.Value.GetContentString());
        }

        protected void AssertUriAndContentDoesNotExist(MemoryStorage storage, Uri uri, string expectedContent)
        {
            var value = storage.Content.FirstOrDefault(pair => pair.Key == uri);

            Assert.Null(value.Key);
        }
    }
}