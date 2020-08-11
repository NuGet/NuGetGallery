// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests.PackageFixup
{
    public class FixPackageHashHandlerFacts
    {
        private static readonly PackageIdentity PackageIdentity = new PackageIdentity("TestUnsigned", new NuGetVersion("1.0.0"));

        private readonly CatalogIndexEntry _packageEntry;
        private readonly Mock<ICloudBlockBlob> _blob;

        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly FixPackageHashHandler _target;

        public FixPackageHashHandlerFacts()
        {
            var messageHandler = new MockServerHttpClientHandler();
            messageHandler.SetAction(
                "/packages/testunsigned.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(File.OpenRead("Packages\\TestUnsigned.1.0.0.nupkg"))
                }
            ));

            _packageEntry = new CatalogIndexEntry(
                new Uri("http://localhost/catalog/entry.json"),
                "nuget:PackageDetails",
                "123",
                DateTime.UtcNow,
                PackageIdentity);

            _blob = new Mock<ICloudBlockBlob>();
            _blob.Setup(b => b.Uri).Returns(new Uri("http://localhost/packages/testunsigned.1.0.0.nupkg"));

            _telemetryService = new Mock<ITelemetryService>();
            _target = new FixPackageHashHandler(
                new HttpClient(messageHandler),
                _telemetryService.Object,
                Mock.Of<ILogger<FixPackageHashHandler>>());
        }

        [Fact]
        public async Task SkipsPackagesThatAlreadyHaveAHash()
        {
            // Arrange
            _blob.Setup(b => b.ContentMD5).Returns("Incorrect MD5 Content Hash");

            // Act
            await _target.ProcessPackageAsync(_packageEntry, _blob.Object);

            // Assert
            _blob.Verify(b => b.FetchAttributesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _telemetryService.Verify(t => t.TrackPackageAlreadyHasHash(PackageIdentity.Id, PackageIdentity.Version), Times.Once);
            _telemetryService.Verify(t => t.TrackPackageHashFixed(PackageIdentity.Id, PackageIdentity.Version), Times.Never);
        }

        [Fact]
        public async Task AddsHashIfPackageIsMissingHash()
        {
            // Arrange
            string hash = null;
            _blob.Setup(b => b.ContentMD5).Returns<string>(null);
            _blob.SetupSet(b => b.ContentMD5 = It.IsAny<string>()).Callback<string>(h => hash = h);
            _blob.Setup(b => b.ETag).Returns("abc");

            // Act
            await _target.ProcessPackageAsync(_packageEntry, _blob.Object);

            // Assert
            Assert.Equal("HwmmE4OAMb2Lr3/Yj7oK6w==", hash);
            _blob.Verify(b => b.FetchAttributesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _blob.Verify(
                b => b.SetPropertiesAsync(
                    It.Is<AccessCondition>(c =>
                        c.IfModifiedSinceTime == null &&
                        c.IfMatchETag == "abc"),
                    null,
                    null),
                Times.Once);

            _telemetryService.Verify(t => t.TrackPackageAlreadyHasHash(PackageIdentity.Id, PackageIdentity.Version), Times.Never);
            _telemetryService.Verify(t => t.TrackPackageHashFixed(PackageIdentity.Id, PackageIdentity.Version), Times.Once);
        }
    }
}