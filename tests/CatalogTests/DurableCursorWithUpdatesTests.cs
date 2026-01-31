// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests
{
    public class DurableCursorWithUpdatesTests
    {
        private readonly Uri _address = new Uri("https://test");
        private readonly DateTime _defaultValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ThrowArgumentOutOfRangeException_maxNumberOfUpdatesToKeep()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DurableCursorWithUpdates(_address, It.IsAny<Storage>(), _defaultValue,
                maxNumberOfUpdatesToKeep: -1, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60)));

            Assert.Equal("maxNumberOfUpdatesToKeep", exception.ParamName);
            Assert.Equal("maxNumberOfUpdatesToKeep must be equal or larger than 0.\r\nParameter name: maxNumberOfUpdatesToKeep", exception.Message);
        }

        [Fact]
        public void ThrowArgumentOutOfRangeException_minIntervalBetweenTwoUpdates()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DurableCursorWithUpdates(_address, It.IsAny<Storage>(), _defaultValue,
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(-1)));

            Assert.Equal("minIntervalBetweenTwoUpdates", exception.ParamName);
            Assert.Equal("minIntervalBetweenTwoUpdates must be equal or larger than 0.\r\nParameter name: minIntervalBetweenTwoUpdates", exception.Message);
        }

        [Fact]
        public async Task SaveAsync_WithDoesNotExistInStorage()
        {
            var storage = new Mock<Storage>(_address);
            StorageContent savedStorageContent = null;
            storage.Setup(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((StringStorageContent) null);
            storage.Setup(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()))
                .Callback<Uri, StorageContent, CancellationToken>((u, sc, c) => savedStorageContent = sc);

            var cursor = new DurableCursorWithUpdates(_address, storage.Object, _defaultValue,
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60));
            cursor.Value = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified);

            await cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(savedStorageContent);
            Assert.IsType<StringStorageContent>(savedStorageContent);
            Assert.Equal("{\"value\":\"2026-01-01T01:00:00.0000000\",\"updates\":[]}", (savedStorageContent as StringStorageContent).Content);

            storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\",\"updates\":[]}")]
        public async Task SaveAsync_WithEmptyUpdatesInStorage(string contentInStorage)
        {
            var storage = new Mock<Storage>(_address);
            StorageContent savedStorageContent = null;
            storage.Setup(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StringStorageContent(contentInStorage, storageDateTimeInUtc: new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc)));
            storage.Setup(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()))
                .Callback<Uri, StorageContent, CancellationToken>((u, sc, c) => savedStorageContent = sc);

            var cursor = new DurableCursorWithUpdates(_address, storage.Object, _defaultValue,
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60));
            cursor.Value = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified);

            await cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(savedStorageContent);
            Assert.IsType<StringStorageContent>(savedStorageContent);
            Assert.Equal("{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                          "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}]}",
                (savedStorageContent as StringStorageContent).Content);

            storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"updateTimeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"updateTimeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}")]
        public async Task SaveAsync(string contentInStorage, string expectedContentInStorageAfterSave)
        {
            var storage = new Mock<Storage>(_address);
            StorageContent savedStorageContent = null;
            storage.Setup(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StringStorageContent(contentInStorage, storageDateTimeInUtc: new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc)));
            storage.Setup(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()))
                .Callback<Uri, StorageContent, CancellationToken>((u, sc, c) => savedStorageContent = sc);

            var cursor = new DurableCursorWithUpdates(_address, storage.Object, _defaultValue,
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60));
            cursor.Value = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified);

            await cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(savedStorageContent);
            Assert.IsType<StringStorageContent>(savedStorageContent);
            Assert.Equal(expectedContentInStorageAfterSave, (savedStorageContent as StringStorageContent).Content);

            storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveAsync_WithoutStorageDateTime()
        {
            var storage = new Mock<Storage>(_address);
            storage.Setup(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StringStorageContent("{\"value\":\"2026-01-01T00:59:00.0000000\"}"));

            var cursor = new DurableCursorWithUpdates(_address, storage.Object, _defaultValue,
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60));
            cursor.Value = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified);

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => cursor.SaveAsync(CancellationToken.None));
            Assert.Equal("storageDateTimeInUtc", exception.ParamName);

            storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
