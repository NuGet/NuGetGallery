// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog.Icons;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Icons
{

    public class IconCopyResultCacheFacts
    {
        [Fact]
        public async Task InitializeAsyncReadsJson()
        {
            Data = "{ \"https://key\": { \"SourceUrl\": \"https://source.test/data\", \"StorageUrl\": \"https://dest.test/data2\", \"IsCopySucceeded\": true } }";

            await Target.InitializeAsync(CancellationToken.None);

            StorageMock.VerifyAll();
            var cache = Target.Get(new Uri("https://key"));
            Assert.NotNull(cache);
            Assert.Equal("https://source.test/data", cache.SourceUrl.AbsoluteUri);
            Assert.Equal("https://dest.test/data2", cache.StorageUrl.AbsoluteUri);
        }

        [Fact]
        public void GetThrowsIfNotInitialized()
        {
            Assert.Throws<InvalidOperationException>(() => Target.Get(new Uri("https://whatever")));
        }

        [Fact]
        public async Task SaveExternalIconThrowsIfNotInitialized()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Target.SaveExternalIcon(new Uri("https://whatever"), new Uri("https://storage.test"), Mock.Of<IStorage>(), Mock.Of<IStorage>(), CancellationToken.None));
        }

        [Fact]
        public void SaveExternalCopyFailureThrowsIfNotInitialized()
        {
            Assert.Throws<InvalidOperationException>(() => Target.SaveExternalCopyFailure(new Uri("https://whatever")));
        }

        [Fact]
        public void ClearThrowsIfNotInitialized()
        {
            Assert.Throws<InvalidOperationException>(() => Target.Clear(new Uri("https://whatever")));
        }

        [Theory]
        [InlineData("https://source/d", null, false)]
        [InlineData("https://source2/d", "https://dest/d", true)]
        public async Task SmokeTest(string sourceUrl, string storageUrlString, bool expectedSuccess)
        {
            Data = "{}";
            await Target.InitializeAsync(CancellationToken.None);

            var storageUrl = storageUrlString == null ? null : new Uri(storageUrlString);
            var success = storageUrlString != null;

            if (success)
            {
                await Target.SaveExternalIcon(new Uri(sourceUrl), storageUrl, StorageMock.Object, IconCacheStorageMock.Object, CancellationToken.None);
                StorageMock
                    .Verify(ics => ics.CopyAsync(storageUrl, IconCacheStorageMock.Object, It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), CancellationToken.None));
            }
            else
            {
                Target.SaveExternalCopyFailure(new Uri(sourceUrl));
            }
            var item = Target.Get(new Uri(sourceUrl));
            Assert.Equal(sourceUrl, item.SourceUrl.AbsoluteUri);
            if (success)
            {
                Assert.True(item.IsCopySucceeded);
                Assert.Equal(DefaultResolvedUrlString, item.StorageUrl.AbsoluteUri);
            }
            else
            {
                Assert.False(item.IsCopySucceeded);
                Assert.Null(item.StorageUrl);
            }
            Assert.Equal(expectedSuccess, item.IsCopySucceeded);

            Target.Clear(new Uri(sourceUrl));
            item = Target.Get(new Uri(sourceUrl));
            Assert.Null(item);
        }

        [Fact]
        public async Task SaveExternalIconDoesNotOverwriteSuccess()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            const string originalIconUrlString = "https://source/";
            var originalIconUrl = new Uri(originalIconUrlString);
            const string firstSuccessStorageUrlString = "https://storage1/d";
            var firstSucessStorageUrl = new Uri(firstSuccessStorageUrlString);
            var secondSuccessStorageUrl = new Uri("https://storage2");

            await Target.SaveExternalIcon(originalIconUrl, firstSucessStorageUrl, StorageMock.Object, IconCacheStorageMock.Object, CancellationToken.None);
            StorageMock
                .Verify(
                    ics => ics.CopyAsync(firstSucessStorageUrl, IconCacheStorageMock.Object, It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), CancellationToken.None),
                    Times.Once);
            await Target.SaveExternalIcon(originalIconUrl, secondSuccessStorageUrl, StorageMock.Object, IconCacheStorageMock.Object, CancellationToken.None);
            StorageMock
                .Verify(
                    ics => ics.CopyAsync(secondSuccessStorageUrl, It.IsAny<IStorage>(), It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
                    Times.Never);

            var item = Target.Get(originalIconUrl);
            Assert.True(item.IsCopySucceeded);
            Assert.Equal(originalIconUrlString, item.SourceUrl.AbsoluteUri);
            Assert.Equal(DefaultResolvedUrlString, item.StorageUrl.AbsoluteUri);
        }

        [Fact]
        public async Task SaveExternalIconOverwritesFailures()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            const string originalIconUrlString = "https://source/";
            var originalIconUrl = new Uri(originalIconUrlString);
            const string successStorageUrlString = "https://storage2/";
            var successStorageUrl = new Uri(successStorageUrlString);

            Target.SaveExternalCopyFailure(originalIconUrl);
            await Target.SaveExternalIcon(originalIconUrl, successStorageUrl, StorageMock.Object, IconCacheStorageMock.Object, CancellationToken.None);
            StorageMock
                .Verify(
                    ics => ics.CopyAsync(successStorageUrl, IconCacheStorageMock.Object, It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), CancellationToken.None),
                    Times.Once);

            var item = Target.Get(originalIconUrl);

            Assert.True(item.IsCopySucceeded);
            Assert.Equal(originalIconUrlString, item.SourceUrl.AbsoluteUri);
            Assert.Equal(DefaultResolvedUrlString, item.StorageUrl.AbsoluteUri);
        }

        [Fact]
        public async Task SavesCache()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            await Target.SaveExternalIcon(new Uri("https://sourcez"), new Uri("https://storage1/d"), StorageMock.Object, IconCacheStorageMock.Object, CancellationToken.None);
            Target.SaveExternalCopyFailure(new Uri("https://sourcey"));

            string savedContent = null;

            StorageMock
                .Setup(s => s.SaveAsync(new Uri("https://cache.test/blob"), It.IsAny<StringStorageContent>(), CancellationToken.None))
                .Callback<Uri, StorageContent, CancellationToken>((_1, sc, _2) => savedContent = ((StringStorageContent)sc).Content)
                .Returns(Task.CompletedTask);

            await Target.SaveAsync(CancellationToken.None);

            StorageMock
                .Verify(s => s.SaveAsync(new Uri("https://cache.test/blob"), It.IsAny<StringStorageContent>(), CancellationToken.None), Times.Once);

            Assert.Contains("https://sourcez", savedContent);
            Assert.DoesNotContain("https://storage1/d", savedContent); // this url is only used for copying blob
            Assert.Contains(DefaultResolvedUrlString, savedContent);
            Assert.Contains("https://sourcey", savedContent);
        }

        public IconCopyResultCacheFacts()
        {
            StorageMock = new Mock<IStorage>();

            StorageMock
                .Setup(s => s.ResolveUri("c2i_cache.json"))
                .Returns(new Uri("https://cache.test/blob"))
                .Verifiable();

            IconCacheStorageMock = new Mock<IStorage>();
            IconCacheStorageMock
                .Setup(ics => ics.ResolveUri(It.IsAny<string>()))
                .Returns(new Uri(DefaultResolvedUrlString));

            var responseStreamContentMock = new Mock<StorageContent>();
            responseStreamContentMock
                .Setup(rsc => rsc.GetContentStream())
                .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(Data)))
                .Verifiable();

            StorageMock
                .Setup(s => s.LoadAsync(new Uri("https://cache.test/blob"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseStreamContentMock.Object);

            Target = new IconCopyResultCache(StorageMock.Object);
        }

        private const string DefaultResolvedUrlString = "https://resolved.test/uri";

        private string Data { get; set; }

        private Mock<IStorage> StorageMock { get; set; }
        private IconCopyResultCache Target { get; set; }
        private Mock<IStorage> IconCacheStorageMock { get; set; }
    }
}
