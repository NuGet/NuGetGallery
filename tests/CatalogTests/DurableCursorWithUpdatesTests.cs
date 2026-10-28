// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests
{
    public class DurableCursorWithUpdatesTests
    {
        private readonly Uri _address = new Uri("https://test");
        private readonly DateTime _defaultValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly Mock<Storage> _storage;
        private readonly DurableCursorWithUpdates _cursor;

        private StringStorageContent _storageContent;
        private StorageContent _savedStorageContent;

        public DurableCursorWithUpdatesTests()
        {
            _storage = new Mock<Storage>(_address);
            _storage.Setup(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _storageContent);
            _storage.Setup(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()))
                .Callback<Uri, StorageContent, CancellationToken>((u, sc, c) => _savedStorageContent = sc);

            _cursor = new DurableCursorWithUpdates(_address, _storage.Object, _defaultValue, Mock.Of<ILogger>(),
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60), minIntervalBeforeToReadUpdate: TimeSpan.FromSeconds(1));
            _cursor.Value = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified);
        }

        [Fact]
        public void ThrowArgumentOutOfRangeException_maxNumberOfUpdatesToKeep()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DurableCursorWithUpdates(_address, It.IsAny<Storage>(), _defaultValue, Mock.Of<ILogger>(),
                maxNumberOfUpdatesToKeep: -1, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60), minIntervalBeforeToReadUpdate: TimeSpan.FromSeconds(1)));

            Assert.Equal("maxNumberOfUpdatesToKeep", exception.ParamName);
            Assert.Equal("maxNumberOfUpdatesToKeep must be equal or larger than 0.\r\nParameter name: maxNumberOfUpdatesToKeep", exception.Message);
        }

        [Fact]
        public void ThrowArgumentOutOfRangeException_minIntervalBetweenTwoUpdates()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DurableCursorWithUpdates(_address, It.IsAny<Storage>(), _defaultValue, Mock.Of<ILogger>(),
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(-1), minIntervalBeforeToReadUpdate: TimeSpan.FromSeconds(1)));

            Assert.Equal("minIntervalBetweenTwoUpdates", exception.ParamName);
            Assert.Equal("minIntervalBetweenTwoUpdates must be equal or larger than 0.\r\nParameter name: minIntervalBetweenTwoUpdates", exception.Message);
        }

        [Fact]
        public void ThrowArgumentOutOfRangeException_minIntervalBeforeToReadUpdate()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DurableCursorWithUpdates(_address, It.IsAny<Storage>(), _defaultValue, Mock.Of<ILogger>(),
                maxNumberOfUpdatesToKeep: 2, minIntervalBetweenTwoUpdates: TimeSpan.FromSeconds(60), minIntervalBeforeToReadUpdate: TimeSpan.FromSeconds(-1)));

            Assert.Equal("minIntervalBeforeToReadUpdate", exception.ParamName);
            Assert.Equal("minIntervalBeforeToReadUpdate must be equal or larger than 0.\r\nParameter name: minIntervalBeforeToReadUpdate", exception.Message);
        }

        [Fact]
        public async Task SaveAsync_WithDoesNotExistInStorage()
        {
            _storageContent = null;
            await _cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(_savedStorageContent);
            Assert.IsType<StringStorageContent>(_savedStorageContent);
            Assert.Equal("{\"value\":\"2026-01-01T01:00:00.0000000\",\"minIntervalBeforeToReadUpdate\":\"00:00:01\",\"updates\":[]}",
                (_savedStorageContent as StringStorageContent).Content);

            _storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\",\"minIntervalBeforeToReadUpdate\":\"00:00:01\"}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\",\"updates\":[]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\",\"minIntervalBeforeToReadUpdate\":\"00:00:01\",\"updates\":[]}")]
        public async Task SaveAsync_WithEmptyUpdatesInStorage(string content)
        {
            _storageContent = new StringStorageContent(content, storageDateTimeInUtc: new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc));
            await _cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(_savedStorageContent);
            Assert.IsType<StringStorageContent>(_savedStorageContent);
            Assert.Equal("{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                          "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                          "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}]}",
                (_savedStorageContent as StringStorageContent).Content);

            _storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveAsync_WithDifferentMinInterval()
        {
            _storageContent = new StringStorageContent("{\"value\":\"2026-01-01T00:59:00.0000000\",\"minIntervalBeforeToReadUpdate\":\"01:01:01\"}",
                storageDateTimeInUtc: new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc));
            await _cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(_savedStorageContent);
            Assert.IsType<StringStorageContent>(_savedStorageContent);
            Assert.Equal("{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                          "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                          "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}]}",
                (_savedStorageContent as StringStorageContent).Content);

            _storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:30.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}")]
        [InlineData("{\"value\":\"2026-01-01T00:59:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}]}",
                    "{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:31.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                  "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}]}")]
        public async Task SaveAsync(string content, string expectedContentAfterSave)
        {
            _storageContent = new StringStorageContent(content, storageDateTimeInUtc: new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc));
            await _cursor.SaveAsync(CancellationToken.None);

            Assert.NotNull(_savedStorageContent);
            Assert.IsType<StringStorageContent>(_savedStorageContent);
            Assert.Equal(expectedContentAfterSave, (_savedStorageContent as StringStorageContent).Content);

            _storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(59, false)]
        [InlineData(60, true)]
        public async Task FlushPendingHistoryAsync_RespectsUpdateInterval(int secondsSinceLastUpdate, bool shouldFlush)
        {
            var now = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc).AddSeconds(secondsSinceLastUpdate);
            var persistedValue = "2026-01-01T01:00:30.0000000";
            _storageContent = new StringStorageContent("{\"value\":\"" + persistedValue + "\"," +
                "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                "\"updates\":[{\"timeStamp\":\"2026-01-01T01:00:00.0000000Z\",\"value\":\"2026-01-01T01:00:00.0000000\"}]}",
                storageDateTimeInUtc: now);

            await _cursor.FlushPendingHistoryAsync(CancellationToken.None);

            if (shouldFlush)
            {
                var saved = JsonConvert.DeserializeObject<CursorValueWithUpdates>(((StringStorageContent)_savedStorageContent).Content);
                Assert.Equal(persistedValue, saved.Value);
                Assert.Equal(persistedValue, saved.Updates[0].Value);
                Assert.Equal(now, saved.Updates[0].TimeStamp);
                Assert.Equal("2026-01-01T01:00:00.0000000", saved.Updates[1].Value);
                _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            else
            {
                _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Fact]
        public async Task FlushPendingHistoryAsync_DoesNotWriteWhenHistoryIsCurrent()
        {
            _storageContent = new StringStorageContent("{\"value\":\"2026-01-01T01:00:30.0000000\"," +
                "\"minIntervalBeforeToReadUpdate\":\"00:00:01\"," +
                "\"updates\":[{\"timeStamp\":\"2026-01-01T01:01:00.0000000Z\",\"value\":\"2026-01-01T01:00:30.0000000\"}]}",
                storageDateTimeInUtc: new DateTime(2026, 1, 1, 1, 2, 0, DateTimeKind.Utc));

            await _cursor.FlushPendingHistoryAsync(CancellationToken.None);

            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FlushPendingHistoryAsync_InitializesMissingHistoryWithoutNewProgress()
        {
            var now = new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc);
            var persistedValue = "2026-01-01T00:59:00.0000000";
            _storageContent = new StringStorageContent("{\"value\":\"" + persistedValue + "\"}", storageDateTimeInUtc: now);

            await _cursor.FlushPendingHistoryAsync(CancellationToken.None);

            var saved = JsonConvert.DeserializeObject<CursorValueWithUpdates>(((StringStorageContent)_savedStorageContent).Content);
            Assert.Equal(persistedValue, saved.Value);
            var update = Assert.Single(saved.Updates);
            Assert.Equal(persistedValue, update.Value);
            Assert.Equal(now, update.TimeStamp);
        }

        [Fact]
        public async Task FlushPendingHistoryAsync_DoesNotCreateAnUninitializedCursor()
        {
            await _cursor.FlushPendingHistoryAsync(CancellationToken.None);

            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SaveAsync_WithoutStorageDateTime()
        {
            _storageContent = new StringStorageContent("{\"value\":\"2026-01-01T00:59:00.0000000\"}");

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => _cursor.SaveAsync(CancellationToken.None));
            Assert.Equal("storageDateTimeInUtc", exception.ParamName);

            _storage.Verify(s => s.LoadStringStorageContentAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
            _storage.Verify(s => s.SaveAsync(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
