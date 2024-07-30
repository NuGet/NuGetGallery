// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery;
using NuGet.Jobs.Validation;
using Moq;
using Xunit;

namespace Validation.Symbols.Tests
{
    public class SymbolsFileServiceTests
    {
        private Mock<ICoreFileStorageService> _packageStorageService;
        private Mock<IFileDownloader> _fileDownloader;
        private Mock<ICoreFileStorageService> _packageValidationStorageService;
        private const string PackageId = "Pack";
        private const string PackageNormalizedVersion = "1.2.3";

        public SymbolsFileServiceTests()
        {
            _packageStorageService = new Mock<ICoreFileStorageService>();
            _fileDownloader = new Mock<IFileDownloader>();
            _packageValidationStorageService = new Mock<ICoreFileStorageService>();
        }

        [Fact]
        public void ConstructorNullCheck()
        {
            // Arrange + Act + Assert
            Assert.Throws<ArgumentNullException>(() => new SymbolsFileService(null, _packageValidationStorageService.Object, _fileDownloader.Object));
            Assert.Throws<ArgumentNullException>(() => new SymbolsFileService(_packageStorageService.Object, null, _fileDownloader.Object));
            Assert.Throws<ArgumentNullException>(() => new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, null));
        }

        [Fact]
        public async Task DownloadSnupkgFileAsyncCallsDownloadValidationPackageFileAsync()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _fileDownloader.Object);
            _fileDownloader.Setup(svss => svss.DownloadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Boo"));
            
            // Act + Assert 
            var exception = await Assert.ThrowsAsync<InvalidOperationException>( async () => await service.DownloadSnupkgFileAsync("https://dummy.snupkg", CancellationToken.None));

            Assert.Equal("Boo", exception.Message);
        }

        [Fact]
        public async Task DownloadNupkgFileAsyncSearchPackageValidationAfterPackageFolder()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _fileDownloader.Object);
            _packageStorageService.Setup(pss => pss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            _packageValidationStorageService.Setup(pvss => pvss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act 
            var stream = await service.DownloadNupkgFileAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

            // Assert
            _packageValidationStorageService.Verify(pss => pss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DownloadNupkgFileAsyncUsesPackageFolderIfFound()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _fileDownloader.Object);
            _packageStorageService.Setup(pss => pss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _packageValidationStorageService.Setup(pvss => pvss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act 
            var stream = await service.DownloadNupkgFileAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

            // Assert
            _packageStorageService.Verify(pss => pss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _packageValidationStorageService.Verify(pss => pss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DownloadNupkgFileAsyncThowsIfNotFound()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _fileDownloader.Object);
            _packageStorageService.Setup(pss => pss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            _packageValidationStorageService.Setup(pvss => pvss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

            // Act + Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.DownloadNupkgFileAsync(PackageId, PackageNormalizedVersion, CancellationToken.None));
        }
    }
}
