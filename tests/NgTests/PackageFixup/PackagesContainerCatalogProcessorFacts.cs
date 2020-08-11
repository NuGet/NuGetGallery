// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests.PackageFixup
{
    public class PackagesContainerCatalogProcessorFacts
    {
        private static readonly PackageIdentity PackageIdentity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));

        private readonly CatalogIndexEntry _catalogEntry;
        private readonly Mock<CloudBlockBlob> _rawBlob;
        private readonly Mock<IPackagesContainerHandler> _handler;
        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly PackagesContainerCatalogProcessor _target;

        public PackagesContainerCatalogProcessorFacts()
        {
            _rawBlob = new Mock<CloudBlockBlob>(MockBehavior.Strict, new Uri("http://localhost/packages/testpackage.1.0.0.nupkg"));

            var container = new Mock<CloudBlobContainer>(MockBehavior.Strict, new Uri("http://localhost/packages/"));
            container.Setup(c => c.GetBlockBlobReference("testpackage.1.0.0.nupkg")).Returns(_rawBlob.Object);

            _catalogEntry = new CatalogIndexEntry(
                new Uri("http://localhost/catalog/entry.json"),
                "nuget:PackageDetails",
                "123",
                DateTime.UtcNow,
                PackageIdentity);

            _handler = new Mock<IPackagesContainerHandler>();
            _telemetryService = new Mock<ITelemetryService>();
            _target = new PackagesContainerCatalogProcessor(
                container.Object,
                _handler.Object,
                _telemetryService.Object,
                Mock.Of<ILogger<PackagesContainerCatalogProcessor>>());
        }

        [Fact]
        public async Task CallsHandler()
        {
            // Act
            await _target.ProcessCatalogIndexEntryAsync(_catalogEntry);

            // Assert
            _handler.Verify(
                h => h.ProcessPackageAsync(
                    _catalogEntry,
                    It.Is<ICloudBlockBlob>(b => b.Uri == _rawBlob.Object.Uri)),
                Times.Once);

            _telemetryService.Verify(
                t => t.TrackHandlerFailedToProcessPackage(
                    It.IsAny<IPackagesContainerHandler>(),
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>()),
                Times.Never);
        }

        [Fact]
        public async Task DoesNotRetryIfHandlerThrowsBlobDoesNotExistException()
        {
            // Arrange
            var storageException = new StorageException(
                new RequestResult
                {
                    HttpStatusCode = (int)HttpStatusCode.NotFound
                },
                "Message",
                inner: null);

            _handler.Setup(h => h.ProcessPackageAsync(It.IsAny<CatalogIndexEntry>(), It.IsAny<ICloudBlockBlob>()))
                .Throws(storageException);

            // Act
            await _target.ProcessCatalogIndexEntryAsync(_catalogEntry);

            // Assert
            _handler.Verify(
                h => h.ProcessPackageAsync(
                    _catalogEntry,
                    It.Is<ICloudBlockBlob>(b => b.Uri == _rawBlob.Object.Uri)),
                Times.Once);

            _telemetryService.Verify(
                t => t.TrackHandlerFailedToProcessPackage(
                    It.IsAny<IPackagesContainerHandler>(),
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>()),
                Times.Never);
        }

        [Fact]
        public async Task DoesNotRetryIfHandlerThrowsUnknownException()
        {
            // Arrange
            var exception = new Exception("Unknown exception!");

            _handler.Setup(h => h.ProcessPackageAsync(It.IsAny<CatalogIndexEntry>(), It.IsAny<ICloudBlockBlob>()))
                .Throws(exception);

            // Act
            await _target.ProcessCatalogIndexEntryAsync(_catalogEntry);

            // Assert
            _handler.Verify(
                h => h.ProcessPackageAsync(
                    _catalogEntry,
                    It.Is<ICloudBlockBlob>(b => b.Uri == _rawBlob.Object.Uri)),
                Times.Once);

            _telemetryService.Verify(
                t => t.TrackHandlerFailedToProcessPackage(
                    It.IsAny<IPackagesContainerHandler>(),
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>()),
                Times.Once);
        }

        [Theory]
        [MemberData(nameof(RetriesIfHandlerThrowsKnownExceptionData))]
        public async Task RetriesIfHandlerThrowsKnownException(Exception exception, bool retries)
        {
            // Arrange
            _handler.Setup(h => h.ProcessPackageAsync(It.IsAny<CatalogIndexEntry>(), It.IsAny<ICloudBlockBlob>()))
                .Throws(exception);

            // Act
            await _target.ProcessCatalogIndexEntryAsync(_catalogEntry);

            // Assert
            _handler.Verify(
                h => h.ProcessPackageAsync(
                    _catalogEntry,
                    It.Is<ICloudBlockBlob>(b => b.Uri == _rawBlob.Object.Uri)),
                retries ? Times.Exactly(5) : Times.Once());
            
            _telemetryService.Verify(
                t => t.TrackHandlerFailedToProcessPackage(
                    It.IsAny<IPackagesContainerHandler>(),
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>()),
                Times.Once);
        }

        [Fact]
        public async Task CanSucceedAfterRetry()
        {
            // Arrange
            var threw = false;
            _handler.Setup(h => h.ProcessPackageAsync(It.IsAny<CatalogIndexEntry>(), It.IsAny<ICloudBlockBlob>()))
                .Callback(() =>
                {
                    if (!threw)
                    {
                        threw = true;
                        throw new TimeoutException();
                    }
                })
                .Returns(Task.CompletedTask);

            // Act
            await _target.ProcessCatalogIndexEntryAsync(_catalogEntry);

            // Assert
            _handler.Verify(
                h => h.ProcessPackageAsync(
                    _catalogEntry,
                    It.Is<ICloudBlockBlob>(b => b.Uri == _rawBlob.Object.Uri)),
               Times.Exactly(2));

            _telemetryService.Verify(
                t => t.TrackHandlerFailedToProcessPackage(
                    It.IsAny<IPackagesContainerHandler>(),
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>()),
                Times.Never);
        }

        public static IEnumerable<object[]> RetriesIfHandlerThrowsKnownExceptionData()
        {
            // Unknown exceptions should not retry.
            yield return new object[]
            {
                new Exception("Unknown exception!"),
                false
            };

            // Failed update due to ETag condition exceptions should retry
            yield return new object[]
            {
                new StorageException(
                    new RequestResult
                    {
                        HttpStatusCode = (int)HttpStatusCode.PreconditionFailed
                    },
                    "Message",
                    inner: null),
                true
            };

            // Timeout exceptions should retry.
            yield return new object[]
            {
                new TaskCanceledException(),
                true
            };

            yield return new object[]
            {
                new TimeoutException(),
                true
            };

            yield return new object[]
            {
                new IOException("IO wrapped exception", new TimeoutException()),
                true
            };

            yield return new object[]
            {
                new SocketException(),
                true
            };

            yield return new object[]
            {
                new IOException("IO wrapped exception", new SocketException()),
                true
            };

            yield return new object[]
            {
                new WebException("Timeout", WebExceptionStatus.Timeout),
                true
            };

            yield return new object[]
            {
                new IOException("IO wrapped exception", new WebException("Timeout", WebExceptionStatus.Timeout)),
                true
            };
        }
    }
}
