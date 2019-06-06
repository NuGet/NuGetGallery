// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ng;
using NgTests.Data;
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
            await aggregateStorageRoot.SaveAsync(new Uri("http://tempuri.org/firstone/test.txt"), new StringStorageContent("test1"), CancellationToken.None);
            await aggregateStorageSub.SaveAsync(new Uri("http://tempuri.org/firstone/sub/test.txt"), new StringStorageContent("test2"), CancellationToken.None);

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

            await aggregateStorageRoot.SaveAsync(new Uri("http://tempuri.org/firstone/test.txt"), new StringStorageContent("test1"), CancellationToken.None);
            await aggregateStorageSub.SaveAsync(new Uri("http://tempuri.org/firstone/sub/test.txt"), new StringStorageContent("test2"), CancellationToken.None);

            // Act
            await aggregateStorageRoot.DeleteAsync(new Uri("http://tempuri.org/firstone/test.txt"), CancellationToken.None);

            // Assert
            Assert.Single(storage1.Content);
            Assert.Single(storage2.Content);
            Assert.Single(storage3.Content);

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

            await aggregateStorage.SaveAsync(new Uri("http://tempuri.org/firstone/test.txt"), new StringStorageContent("test1"), CancellationToken.None);

            // Act
            var content1 = await aggregateStorage.LoadAsync(new Uri("http://tempuri.org/firstone/test.txt"), CancellationToken.None);
            var content2 = await aggregateStorage.LoadAsync(new Uri("http://tempuri.org/secondone/test.txt"), CancellationToken.None);
            var content3 = await aggregateStorage.LoadAsync(new Uri("http://tempuri.org/thirdone/test.txt"), CancellationToken.None);

            // Assert
            Assert.NotNull(content1);
            Assert.Null(content2);
            Assert.Null(content3);
        }

        [Fact]
        public async Task CorrectlyReplacesRegistrationBaseUrlsInSecondaryStorage()
        {
            // Arrange
            var storageToReplay = Registrations.CreateTestRegistrations();
            var storage1 = new MemoryStorage(storageToReplay.BaseAddress);
            var storage2 = new MemoryStorage(new Uri("http://tempuri.org/secondone"));
            var storage3 = new MemoryStorage(new Uri("http://tempuri.org/thirdone"));

            var secondaryStorageBaseUrlRewriter = new SecondaryStorageBaseUrlRewriter(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(storage1.BaseAddress.ToString(), storage2.BaseAddress.ToString() ),
                new KeyValuePair<string, string>(storage1.BaseAddress.ToString(), storage3.BaseAddress.ToString() )
            });

            var aggregateStorageFactory = CreateWithInterceptor(secondaryStorageBaseUrlRewriter.Rewrite, storage1, storage2, storage3);
            var aggregateStorage = aggregateStorageFactory.Create();

            var storage1BaseAddress = storage1.BaseAddress.ToString();
            var storage2BaseAddress = storage2.BaseAddress.ToString();
            var storage3BaseAddress = storage3.BaseAddress.ToString();

            // Act
            foreach (var content in storageToReplay.Content)
            {
                await aggregateStorage.SaveAsync(content.Key, content.Value, CancellationToken.None);
            }

            // Assert
            Assert.Equal(storageToReplay.Content.Count, storage1.Content.Count);
            Assert.Equal(storageToReplay.Content.Count, storage2.Content.Count);
            Assert.Equal(storageToReplay.Content.Count, storage3.Content.Count);

            foreach (var content in storage1.Content)
            {
                AssertContentDoesNotContain(content.Value, storage1BaseAddress);
                AssertContentDoesNotContain(content.Value, storage2BaseAddress);
                AssertContentDoesNotContain(content.Value, storage3BaseAddress);
            }

            foreach (var content in storage2.Content)
            {
                AssertContentDoesNotContain(content.Value, storage1BaseAddress);
                AssertContentDoesNotContain(content.Value, storage2BaseAddress);
                AssertContentDoesNotContain(content.Value, storage3BaseAddress);
            }

            foreach (var content in storage3.Content)
            {
                AssertContentDoesNotContain(content.Value, storage1BaseAddress);
                AssertContentDoesNotContain(content.Value, storage2BaseAddress);
                AssertContentDoesNotContain(content.Value, storage3BaseAddress);
            }
        }

        protected AggregateStorageFactory Create(MemoryStorage primaryStorage, MemoryStorage secondaryStorage, params MemoryStorage[] storages)
        {
            const AggregateStorage.WriteSecondaryStorageContentInterceptor interceptor = null;

            return CreateWithInterceptor(interceptor, primaryStorage, secondaryStorage, storages);
        }

        protected AggregateStorageFactory CreateWithInterceptor(AggregateStorage.WriteSecondaryStorageContentInterceptor interceptor, MemoryStorage primaryStorage, MemoryStorage secondaryStorage, params MemoryStorage[] storages)
        {
            var storageFactories = new List<StorageFactory>();
            storageFactories.Add(new TestStorageFactory(name => primaryStorage.WithName(name)));
            storageFactories.Add(new TestStorageFactory(name => secondaryStorage.WithName(name)));

            foreach (var storage in storages)
            {
                storageFactories.Add(new TestStorageFactory(name => storage.WithName(name)));
            }

            return new AggregateStorageFactory(
                storageFactories.First(),
                storageFactories.Skip(1).ToArray(),
                interceptor,
                verbose: false);
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

        protected void AssertContentDoesNotContain(StorageContent content, string value)
        {
            var contentValue = content.GetContentString();

            Assert.DoesNotContain(contentValue, value, StringComparison.OrdinalIgnoreCase);
        }
    }
}