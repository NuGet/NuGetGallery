// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NuGet.Services.Storage;
using Xunit;

namespace NuGet.Services.Cursor.Tests
{
    public class DurableCursorFacts
    {
        [Fact]
        public async Task SavesToStorage()
        {
            var storageMock = CreateStorageMock();
            storageMock
                .Protected().Setup<Task>("OnSave", ItExpr.IsAny<Uri>(), ItExpr.IsAny<StorageContent>(), true, ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult(0))
                .Verifiable();

            var durableCursor = new DurableCursor(new Uri("http://localhost/cursor.json"), storageMock.Object, new DateTimeOffset(2017, 5, 5, 17, 8, 42, TimeSpan.Zero));
            await durableCursor.Save(CancellationToken.None);
            storageMock.Verify();
        }

        [Fact]
        public async Task LoadsFromStorage()
        {
            var storageMock = CreateStorageMock();
            storageMock
                .Protected().Setup<Task<StorageContent>>("OnLoad", ItExpr.IsAny<Uri>(), ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult<StorageContent>(null))
                .Verifiable();

            var durableCursor = new DurableCursor(new Uri("http://localhost/cursor.json"), storageMock.Object, new DateTimeOffset(2017, 5, 5, 17, 8, 42, TimeSpan.Zero));
            await durableCursor.Load(CancellationToken.None);
            storageMock.Verify();
        }

        [Fact]
        public async Task UsesDefaultValue()
        {
            var storageMock = CreateStorageMock();
            storageMock
                .Protected().Setup<Task<StorageContent>>("OnLoad", ItExpr.IsAny<Uri>(), ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult<StorageContent>(null));

            DateTimeOffset defaultValue = new DateTimeOffset(2017, 5, 5, 17, 8, 42, TimeSpan.Zero);
            var durableCursor = new DurableCursor(new Uri("http://localhost/cursor.json"), storageMock.Object, defaultValue);
            await durableCursor.Load(CancellationToken.None);

            Assert.Equal(defaultValue, durableCursor.Value);
        }

        [Fact]
        public async Task CanReadWhatItSaves()
        {
            StorageContent savedContent = null;

            var storageMock = CreateStorageMock();
            storageMock
                .Protected().Setup<Task>("OnSave", ItExpr.IsAny<Uri>(), ItExpr.IsAny<StorageContent>(), true, ItExpr.IsAny<CancellationToken>())
                .Callback<Uri, StorageContent, bool, CancellationToken>((uri, content, overwrite, token) => { savedContent = content; })
                .Returns(Task.FromResult(0));

            storageMock
                .Protected().Setup<Task<StorageContent>>("OnLoad", ItExpr.IsAny<Uri>(), ItExpr.IsAny<CancellationToken>())
                .Returns<Uri, CancellationToken>((uri, token) => Task.FromResult(savedContent));

            DateTimeOffset defaultValue = new DateTimeOffset(2017, 5, 5, 17, 8, 42, TimeSpan.Zero);
            DateTimeOffset actualValue = new DateTimeOffset(2017, 5, 5, 17, 49, 42, TimeSpan.Zero);
            var durableCursorSaver = new DurableCursor(new Uri("http://localhost/cursor.json"), storageMock.Object, defaultValue);
            durableCursorSaver.Value = actualValue;
            await durableCursorSaver.Save(CancellationToken.None);

            var durableCursorLoader = new DurableCursor(new Uri("http://localhost/cursor.json"), storageMock.Object, defaultValue);
            await durableCursorLoader.Load(CancellationToken.None);

            Assert.Equal(actualValue, durableCursorLoader.Value);
        }

        private static Mock<Storage.Storage> CreateStorageMock()
        {
            var loggerMock = new Mock<ILogger<Storage.Storage>>();

            return new Mock<Storage.Storage>(MockBehavior.Strict, new Uri("http://localhost/"), loggerMock.Object);
        }
    }
}
