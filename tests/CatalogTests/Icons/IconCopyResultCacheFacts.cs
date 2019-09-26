// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public void SetThrowsIfNotInitialized()
        {
            Assert.Throws<InvalidOperationException>(() => Target.Set(new Uri("https://whatever"), ExternalIconCopyResult.Fail(new Uri("https://another"))));
        }

        [Fact]
        public void ClearThrowsIfNotInitialized()
        {
            Assert.Throws<InvalidOperationException>(() => Target.Clear(new Uri("https://whatever"), new Uri("https://another")));
        }

        [Theory]
        [InlineData("https://source/d", null, false)]
        [InlineData("https://source2/d", "https://dest/d", true)]
        public async Task SmokeTest(string sourceUrl, string storageUrlString, bool expectedSuccess)
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            var storageUrl = storageUrlString == null ? null : new Uri(storageUrlString);
            Target.Set(new Uri(sourceUrl), new ExternalIconCopyResult { SourceUrl = new Uri(sourceUrl), StorageUrl = storageUrl });
            var item = Target.Get(new Uri(sourceUrl));
            Assert.Equal(sourceUrl, item.SourceUrl.AbsoluteUri);
            if (storageUrlString == null)
            {
                Assert.Null(item.StorageUrl);
            }
            else
            {
                Assert.Equal(storageUrlString, item.StorageUrl.AbsoluteUri);
            }
            Assert.Equal(expectedSuccess, item.IsCopySucceeded);

            Target.Clear(new Uri(sourceUrl), storageUrl);
            item = Target.Get(new Uri(sourceUrl));
            Assert.Null(item);
        }

        [Fact]
        public async Task SetDoesNotOverwrite()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            Target.Set(new Uri("https://source"), ExternalIconCopyResult.Success(new Uri("https://source1/d"), new Uri("https://storage1/d")));
            Target.Set(new Uri("https://source"), ExternalIconCopyResult.Success(new Uri("https://source2"), new Uri("https://storage2")));
            var item = Target.Get(new Uri("https://source"));

            Assert.Equal("https://source1/d", item.SourceUrl.AbsoluteUri);
            Assert.Equal("https://storage1/d", item.StorageUrl.AbsoluteUri);
        }

        [Fact]
        public async Task ClearsWhenStorageMatches()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            Target.Set(new Uri("https://source"), ExternalIconCopyResult.Success(new Uri("https://source1"), new Uri("https://storage1")));
            var item = Target.Get(new Uri("https://source"));
            Assert.NotNull(item);
            Target.Clear(new Uri("https://source"), new Uri("https://storage1"));
            item = Target.Get(new Uri("https://source"));
            Assert.Null(item);
        }

        [Fact]
        public async Task NotClearsWhenStorageNotMatch()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            Target.Set(new Uri("https://source"), ExternalIconCopyResult.Success(new Uri("https://source1/d"), new Uri("https://storage1/d")));
            var item = Target.Get(new Uri("https://source"));
            Assert.NotNull(item);
            Target.Clear(new Uri("https://source"), new Uri("https://storage2"));
            item = Target.Get(new Uri("https://source"));
            Assert.NotNull(item);
            Assert.Equal("https://source1/d", item.SourceUrl.AbsoluteUri);
            Assert.Equal("https://storage1/d", item.StorageUrl.AbsoluteUri);
        }

        [Fact]
        public async Task SavesCache()
        {
            Data = "{}";

            await Target.InitializeAsync(CancellationToken.None);

            Target.Set(new Uri("https://sourcez"), ExternalIconCopyResult.Success(new Uri("https://source1/d"), new Uri("https://storage1/d")));
            Target.Set(new Uri("https://sourcey"), ExternalIconCopyResult.Success(new Uri("https://source2/d"), new Uri("https://storage2/d")));

            string savedContent = null;

            StorageMock
                .Setup(s => s.SaveAsync(new Uri("https://cache.test/blob"), It.IsAny<StringStorageContent>(), CancellationToken.None))
                .Callback<Uri, StorageContent, CancellationToken>((_1, sc, _2) => savedContent = ((StringStorageContent)sc).Content)
                .Returns(Task.CompletedTask);

            await Target.SaveAsync(CancellationToken.None);

            StorageMock
                .Verify(s => s.SaveAsync(new Uri("https://cache.test/blob"), It.IsAny<StringStorageContent>(), CancellationToken.None), Times.Once);

            Assert.Contains("https://sourcez", savedContent);
            Assert.Contains("https://sourcey", savedContent);
            Assert.Contains("https://source1/d", savedContent);
            Assert.Contains("https://source2/d", savedContent);
            Assert.Contains("https://storage1/d", savedContent);
            Assert.Contains("https://storage2/d", savedContent);
        }

        public IconCopyResultCacheFacts()
        {
            StorageMock = new Mock<IStorage>();

            StorageMock
                .Setup(s => s.ResolveUri("c2i_cache.json"))
                .Returns(new Uri("https://cache.test/blob"))
                .Verifiable();

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

        private string Data { get; set; }

        private Mock<IStorage> StorageMock { get; set; }
        private IconCopyResultCache Target { get; set; }
    }
}
