// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Helpers
{
    public class CatalogPropertiesTests
    {
        [Fact]
        public void Constructor_InitializesPropertiesWithNull()
        {
            var properties = new CatalogProperties(lastCreated: null, lastDeleted: null, lastEdited: null);

            Assert.Null(properties.LastCreated);
            Assert.Null(properties.LastDeleted);
            Assert.Null(properties.LastEdited);
        }

        [Fact]
        public void Constructor_InitializesPropertiesWithNonNullValues()
        {
            var lastCreated = DateTime.Now;
            var lastDeleted = lastCreated.AddMinutes(1);
            var lastEdited = lastDeleted.AddMinutes(1);
            var properties = new CatalogProperties(lastCreated, lastDeleted, lastEdited);

            Assert.Equal(lastCreated, properties.LastCreated);
            Assert.Equal(lastDeleted, properties.LastDeleted);
            Assert.Equal(lastEdited, properties.LastEdited);
        }

        [Fact]
        public async Task ReadAsync_ThrowsForNullStorage()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => CatalogProperties.ReadAsync(
                    storage: null,
                    telemetryService: Mock.Of<ITelemetryService>(),
                    cancellationToken: CancellationToken.None));

            Assert.Equal("storage", exception.ParamName);
        }

        [Fact]
        public async Task ReadAsync_ThrowsForNullTelemetryService()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => CatalogProperties.ReadAsync(
                    storage: Mock.Of<IStorage>(),
                    telemetryService: null,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("telemetryService", exception.ParamName);
        }

        [Fact]
        public async Task ReadAsync_ThrowsIfCancelled()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => CatalogProperties.ReadAsync(
                    Mock.Of<IStorage>(),
                    Mock.Of<ITelemetryService>(),
                    new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task ReadAsync_ReturnsNullPropertiesIfStorageReturnsNull()
        {
            var storage = CreateStorageMock(json: null);

            var catalogProperties = await CatalogProperties.ReadAsync(
                storage.Object,
                Mock.Of<ITelemetryService>(),
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            Assert.Null(catalogProperties.LastCreated);
            Assert.Null(catalogProperties.LastDeleted);
            Assert.Null(catalogProperties.LastEdited);

            storage.Verify();
        }

        [Fact]
        public async Task ReadAsync_ReturnsNullPropertiesIfPropertiesMissing()
        {
            var storage = CreateStorageMock(json: "{}");

            var catalogProperties = await CatalogProperties.ReadAsync(
                storage.Object,
                Mock.Of<ITelemetryService>(),
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            Assert.Null(catalogProperties.LastCreated);
            Assert.Null(catalogProperties.LastDeleted);
            Assert.Null(catalogProperties.LastEdited);

            storage.Verify();
        }

        [Fact]
        public async Task ReadAsync_ReturnsAllPropertiesIfAllPropertiesSet()
        {
            var lastCreatedDatetimeOffset = CreateDateTimeOffset(TimeSpan.FromMinutes(-5));
            var lastDeletedDatetimeOffset = CreateDateTimeOffset(TimeSpan.Zero);
            var lastEditedDatetimeOffset = CreateDateTimeOffset(TimeSpan.FromMinutes(-3));
            var json = $"{{\"nuget:lastCreated\":\"{(lastCreatedDatetimeOffset.ToString("O"))}\"," +
                $"\"nuget:lastDeleted\":\"{(lastDeletedDatetimeOffset.ToString("O"))}\"," +
                $"\"nuget:lastEdited\":\"{(lastEditedDatetimeOffset.ToString("O"))}\"}}";
            var storage = CreateStorageMock(json);

            var catalogProperties = await CatalogProperties.ReadAsync(
                storage.Object,
                Mock.Of<ITelemetryService>(),
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            Assert.NotNull(catalogProperties.LastCreated);
            Assert.NotNull(catalogProperties.LastDeleted);
            Assert.NotNull(catalogProperties.LastEdited);
            Assert.Equal(lastCreatedDatetimeOffset, catalogProperties.LastCreated.Value);
            Assert.Equal(lastDeletedDatetimeOffset, catalogProperties.LastDeleted.Value);
            Assert.Equal(lastEditedDatetimeOffset, catalogProperties.LastEdited.Value);

            storage.Verify();
        }

        [Fact]
        public async Task ReadAsync_ReturnsDateTimeWithFractionalHourOffsetInUtc()
        {
            var lastCreated = CreateDateTimeOffset(new TimeSpan(hours: 5, minutes: 30, seconds: 0));
            var json = CreateCatalogJson("nuget:lastCreated", lastCreated);

            await VerifyPropertyAsync(json, catalogProperties =>
            {
                Assert.NotNull(catalogProperties.LastCreated);
                Assert.Equal(lastCreated, catalogProperties.LastCreated.Value);
                Assert.Equal(DateTimeKind.Utc, catalogProperties.LastCreated.Value.Kind);
            });
        }

        [Fact]
        public async Task ReadAsync_ReturnsDateTimeWithPositiveOffsetInUtc()
        {
            var lastDeleted = CreateDateTimeOffset(TimeSpan.FromHours(1));
            var json = CreateCatalogJson("nuget:lastDeleted", lastDeleted);

            await VerifyPropertyAsync(json, catalogProperties =>
            {
                Assert.NotNull(catalogProperties.LastDeleted);
                Assert.Equal(lastDeleted, catalogProperties.LastDeleted.Value);
                Assert.Equal(DateTimeKind.Utc, catalogProperties.LastDeleted.Value.Kind);
            });
        }

        [Fact]
        public async Task ReadAsync_ReturnsDateTimeWithNegativeOffsetInUtc()
        {
            var lastEdited = CreateDateTimeOffset(TimeSpan.FromHours(-1));
            var json = CreateCatalogJson("nuget:lastEdited", lastEdited);

            await VerifyPropertyAsync(json, catalogProperties =>
            {
                Assert.NotNull(catalogProperties.LastEdited);
                Assert.Equal(lastEdited, catalogProperties.LastEdited.Value);
                Assert.Equal(DateTimeKind.Utc, catalogProperties.LastEdited.Value.Kind);
            });
        }

        [Fact]
        public async Task ReadAsync_ReturnUtcDateTimeInUtc()
        {
            var lastCreated = DateTimeOffset.UtcNow;
            var json = CreateCatalogJson("nuget:lastCreated", lastCreated);

            await VerifyPropertyAsync(json, catalogProperties =>
            {
                Assert.NotNull(catalogProperties.LastCreated);
                Assert.Equal(lastCreated, catalogProperties.LastCreated.Value);
                Assert.Equal(DateTimeKind.Utc, catalogProperties.LastCreated.Value.Kind);
            });
        }

        private static string CreateCatalogJson(string propertyName, DateTimeOffset propertyValue)
        {
            return $"{{\"{propertyName}\":\"{(propertyValue.ToString("O"))}\"}}";
        }

        private static DateTimeOffset CreateDateTimeOffset(TimeSpan offset)
        {
            var datetime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Unspecified);

            return new DateTimeOffset(datetime, offset);
        }

        private static async Task VerifyPropertyAsync(string json, Action<CatalogProperties> propertyVerifier)
        {
            var storage = CreateStorageMock(json);

            var catalogProperties = await CatalogProperties.ReadAsync(
                storage.Object,
                Mock.Of<ITelemetryService>(),
                CancellationToken.None);

            Assert.NotNull(catalogProperties);
            propertyVerifier(catalogProperties);
            storage.Verify();
        }

        private static Mock<IStorage> CreateStorageMock(string json)
        {
            var storage = new Mock<IStorage>(MockBehavior.Strict);

            storage.Setup(x => x.ResolveUri(It.IsNotNull<string>()))
                .Returns(new Uri("https://unit.test"))
                .Verifiable();
            storage.Setup(x => x.LoadStringAsync(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(json)
                .Verifiable();

            return storage;
        }
    }
}