// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery;
using Moq;
using Xunit;

namespace Validation.Symbols.Tests
{
    public class SymbolsFileServiceTests
    {
        private Mock<ICoreFileStorageService> _packageStorageService;
        private Mock<ICoreFileStorageService> _symbolsValidationStorageService;
        private Mock<ICoreFileStorageService> _packageValidationStorageService;
        private const string PackageId = "Pack";
        private const string PackageNormalizedVersion = "1.2.3";

        public SymbolsFileServiceTests()
        {
            _packageStorageService = new Mock<ICoreFileStorageService>();
            _symbolsValidationStorageService = new Mock<ICoreFileStorageService>();
            _packageValidationStorageService = new Mock<ICoreFileStorageService>();
        }

        [Fact]
        public void ConstructorNullCheck()
        {
            // Arrange + Act + Assert
            Assert.Throws<ArgumentNullException>(() => new SymbolsFileService(null, _packageValidationStorageService.Object, _symbolsValidationStorageService.Object));
            Assert.Throws<ArgumentNullException>(() => new SymbolsFileService(_packageStorageService.Object, null, _symbolsValidationStorageService.Object));
            Assert.Throws<ArgumentNullException>(() => new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, null));
        }

        [Fact]
        public async Task DownloadSnupkgFileAsyncCallsDownloadValidationPackageFileAsync()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _symbolsValidationStorageService.Object);
            _symbolsValidationStorageService.Setup(svss => svss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            // Act 
            var stream = await service.DownloadSnupkgFileAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

            // Assert
            _symbolsValidationStorageService.Verify(svss => svss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        }

        [Fact]
        public async Task DownloadNupkgFileAsyncSearchPackageValidationAfterPackageFolder()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _symbolsValidationStorageService.Object);
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
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _symbolsValidationStorageService.Object);
            _packageStorageService.Setup(pss => pss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _packageValidationStorageService.Setup(pvss => pvss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act 
            var stream = await service.DownloadNupkgFileAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

            // Assert
            _packageStorageService.Verify(pss => pss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _packageValidationStorageService.Verify(pss => pss.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void DownloadNupkgFileAsyncThowsIfNotFound()
        {
            // Arrange
            var service = new SymbolsFileService(_packageStorageService.Object, _packageValidationStorageService.Object, _symbolsValidationStorageService.Object);
            _packageStorageService.Setup(pss => pss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            _packageValidationStorageService.Setup(pvss => pvss.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

            // Act + Assert
            Assert.ThrowsAsync<FileNotFoundException>(() => service.DownloadNupkgFileAsync(PackageId, PackageNormalizedVersion, CancellationToken.None));
        }
    }
}
