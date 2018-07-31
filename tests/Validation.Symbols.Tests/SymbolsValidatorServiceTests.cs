// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Jobs.Validation.Symbols.Core;
using Moq;
using Xunit;

namespace Validation.Symbols.Tests
{
    public class SymbolsValidatorServiceTests
    {
        private const string PackageId = "Pack";
        private const string PackageNormalizedVersion = "1.2.3";

        public sealed class TheValidateSymbolsAsyncMethod : FactBase
        {
            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfSnupkgNotFound()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ThrowsAsync(new FileNotFoundException("Snupkg not found"));

                var service = new SymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger.Object);

                // Act 
                var  result = await service.ValidateSymbolsAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

                // Assert 
                Assert.Equal(ValidationResult.Failed.Status, result.Status);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfNupkgNotFound()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ThrowsAsync(new FileNotFoundException("Nupkg not found"));

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                var service = new SymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger.Object);

                // Act 
                var result = await service.ValidateSymbolsAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

                // Assert 
                Assert.Equal(ValidationResult.Failed.Status, result.Status);
            }

            [Fact]

            public async Task ValidateSymbolsAsyncWillFailIfTheSnupkgNotSubsetOfNupkg()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), It.IsAny<string[]>())).Returns(new List<string>()
                { "foo.dll" });

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), ".pdb")).Returns(new List<string>()
                { "bar.pdb" });

                var service = new SymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger.Object);

                // Act 
                var result = await service.ValidateSymbolsAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

                // Assert 
                Assert.Equal(ValidationResult.Failed.Status, result.Status);
                Assert.Equal(1, result.Issues.Count);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillSucceedOnCorrectMatch()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), It.IsAny<string[]>())).Returns(new List<string>()
                { "foo.dll" });

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), It.IsAny<string[]>())).Returns(new List<string>()
                { "foo.pdb" });

                var service = new TestSymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger.Object);

                // Act 
                var result = await service.ValidateSymbolsAsync(PackageId, PackageNormalizedVersion, CancellationToken.None);

                // Assert 
                Assert.Equal(ValidationResult.Succeeded.Status, result.Status);
                Assert.True(service.ValidateSymbolMatchingInvoked);
            }
        }

        public class TheSymbolsHaveMatchingPEFilesMethod
        {
            [Fact]
            public void SymbolsHaveMatchingPEFilesReturnsTrueWhenPdbsMatchPEFiles()
            {
                // Arrange
                string[] symbols = new string[] { @"lib\math\foo.pdb" , @"lib\math\bar.pdb" };
                string[] pes = new string[] { @"lib\math\foo.dll", @"lib\math\bar.exe", @"lib\math2\bar2.exe" };

                // Act
                var result = SymbolsValidatorService.SymbolsHaveMatchingPEFiles(symbols, pes);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void SymbolsHaveMatchingPEFilesReturnsFalseWhenPdbsDoNotMatchPEFiles()
            {
                // Arrange
                string[] symbols = new string[] { @"lib\math\foo.pdb", @"lib\math1\bar.pdb" };
                string[] pes = new string[] { @"lib\math\foo.dll", @"lib\math\bar.exe", @"lib\math2\bar2.exe" };

                // Act
                var result = SymbolsValidatorService.SymbolsHaveMatchingPEFiles(symbols, pes);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void SymbolsHaveMatchingNullCheck()
            {
                // Arrange
                string[] symbols = new string[] { @"lib\math\foo.pdb", @"lib\math1\bar.pdb" };
                string[] pes = new string[] { @"lib\math\foo.dll", @"lib\math\bar.exe", @"lib\math2\bar2.exe" };

                // Act + Assert
                Assert.Throws<ArgumentNullException>(()=>SymbolsValidatorService.SymbolsHaveMatchingPEFiles(null, pes));
                Assert.Throws<ArgumentNullException>(() => SymbolsValidatorService.SymbolsHaveMatchingPEFiles(symbols, null));
            }
        }

        public class FactBase
        {
            public Mock<ISymbolsFileService> _symbolsFileService;
            public Mock<ITelemetryService> _telemetryService;
            public Mock<ILogger<SymbolsValidatorService>> _logger;
            public Mock<IZipArchiveService> _zipService;

            public FactBase()
            {
                _symbolsFileService = new Mock<ISymbolsFileService>();
                _telemetryService = new Mock<ITelemetryService>();
                _logger = new Mock<ILogger<SymbolsValidatorService>>();
                _zipService = new Mock<IZipArchiveService>();
            }
        }

        private class TestSymbolsValidatorService : SymbolsValidatorService
        {
            public bool ValidateSymbolMatchingInvoked = false;

            public TestSymbolsValidatorService(ISymbolsFileService symbolFileService, IZipArchiveService zipArchiveService, ITelemetryService telemetryService, ILogger<SymbolsValidatorService> logger) :
                base(symbolFileService, zipArchiveService, telemetryService, logger)
            {
            }

            public override IValidationResult ValidateSymbolMatching(string targetDirectory, string packageId, string packageNormalizedVersion)
            {
                ValidateSymbolMatchingInvoked = true;
                return ValidationResult.Succeeded;
            }
        }
    }
}
