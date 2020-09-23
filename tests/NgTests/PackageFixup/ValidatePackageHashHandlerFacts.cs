// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests.PackageFixup
{
    public class ValidatePackageHashHandlerFacts
    {
        private static readonly PackageIdentity PackageIdentity = new PackageIdentity("TestUnsigned", new NuGetVersion("1.0.0"));

        private readonly CatalogIndexEntry _packageEntry;
        private readonly Mock<ICloudBlockBlob> _blob;

        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly ValidatePackageHashHandler _target;

        public ValidatePackageHashHandlerFacts()
        {
            var messageHandler = new MockServerHttpClientHandler();
            messageHandler.SetAction(
                "/packages/testunsigned.1.0.0.nupkg",
                request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(File.OpenRead("Packages\\TestUnsigned.1.0.0.nupkg.testdata"))
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
            _target = new ValidatePackageHashHandler(
                new HttpClient(messageHandler),
                _telemetryService.Object,
                Mock.Of<ILogger<ValidatePackageHashHandler>>());
        }

        [Fact]
        public async Task ReportsPackagesThatAreMissingAHash()
        {
            // Arrange
            _blob.Setup(b => b.ContentMD5).Returns<string>(null);

            // Act
            await _target.ProcessPackageAsync(_packageEntry, _blob.Object);

            // Assert
            _blob.Verify(b => b.FetchAttributesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _telemetryService.Verify(t => t.TrackPackageMissingHash(PackageIdentity.Id, PackageIdentity.Version), Times.Once);
            _telemetryService.Verify(t => t.TrackPackageHasIncorrectHash(PackageIdentity.Id, PackageIdentity.Version), Times.Never);
        }

        [Fact]
        public async Task ReportsPackagesWithIncorrectHash()
        {
            // Arrange
            _blob.Setup(b => b.ContentMD5).Returns("Incorrect MD5 Content Hash");

            // Act
            await _target.ProcessPackageAsync(_packageEntry, _blob.Object);

            // Assert
            _blob.Verify(b => b.FetchAttributesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _telemetryService.Verify(t => t.TrackPackageMissingHash(PackageIdentity.Id, PackageIdentity.Version), Times.Never);
            _telemetryService.Verify(t => t.TrackPackageHasIncorrectHash(PackageIdentity.Id, PackageIdentity.Version), Times.Once);
        }

        [Fact]
        public async Task ReportsNothingIfPackageHasCorrectHash()
        {
            // Arrange
            _blob.Setup(b => b.ContentMD5).Returns("HwmmE4OAMb2Lr3/Yj7oK6w==");

            // Act
            await _target.ProcessPackageAsync(_packageEntry, _blob.Object);

            // Assert
            _blob.Verify(b => b.FetchAttributesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _telemetryService.Verify(t => t.TrackPackageMissingHash(PackageIdentity.Id, PackageIdentity.Version), Times.Never);
            _telemetryService.Verify(t => t.TrackPackageHasIncorrectHash(PackageIdentity.Id, PackageIdentity.Version), Times.Never);
        }
    }
}
