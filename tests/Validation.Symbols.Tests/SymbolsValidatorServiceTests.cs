// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Jobs.Validation.Symbols.Core;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Validation.Symbols.Tests
{
    public class SymbolsValidatorServiceTests
    {
        private const string PackageId = "Pack";
        private const string PackageNormalizedVersion = "1.2.3";
        private static readonly SymbolsValidatorMessage Message = new SymbolsValidatorMessage(new Guid(), 1, PackageId, PackageNormalizedVersion, "https://dummy.snupkg");

        public sealed class TheValidateSymbolMatchingMethod : FactBase, IDisposable
        {
            public TheValidateSymbolMatchingMethod(ITestOutputHelper output) : base(output)
            {
                Target = new SymbolsValidatorService(
                    _symbolsFileService.Object,
                    _zipService.Object,
                    _telemetryService.Object,
                    _logger);

                Directory = TestDirectory.Create();
            }

            public SymbolsValidatorService Target { get; }
            public TestDirectory Directory { get; }

            public void Dispose()
            {
                Directory.Dispose();
            }

            [Fact]
            public void MatchingDllAndPortablePdbPassValidation()
            {
                File.WriteAllBytes(Path.Combine(Directory, "testlib.dll"), TestData.BaselineDll);
                File.WriteAllBytes(Path.Combine(Directory, "testlib.pdb"), TestData.BaselinePdb);

                var result = Target.ValidateSymbolMatching(Directory, "TestLib", "1.0.0");

                Assert.Equal(ValidationStatus.Succeeded, result.Status);
            }

            [Fact]
            public void MismatchedDllAndPortablePdbFailValidation()
            {
                File.WriteAllBytes(Path.Combine(Directory, "testlib.dll"), TestData.AddClassDll);
                File.WriteAllBytes(Path.Combine(Directory, "testlib.pdb"), TestData.BaselinePdb);

                var result = Target.ValidateSymbolMatching(Directory, "TestLib", "1.0.0");

                Assert.Equal(ValidationStatus.Failed, result.Status);
                Assert.Equal(ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound, Assert.Single(result.Issues).IssueCode);
            }

            [Fact]
            public void MatchingDllAndWindowsPdbFailValidation()
            {
                File.WriteAllBytes(Path.Combine(Directory, "testlib.dll"), TestData.WindowsDll);
                File.WriteAllBytes(Path.Combine(Directory, "testlib.pdb"), TestData.WindowsPdb);

                var result = Target.ValidateSymbolMatching(Directory, "TestLib", "1.0.0");

                Assert.Equal(ValidationStatus.Failed, result.Status);
                Assert.Equal(ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable, Assert.Single(result.Issues).IssueCode);
            }
        }

        public sealed class TheValidateSymbolsAsyncMethod : FactBase
        {
            public TheValidateSymbolsAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfSnupkgNotFound()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ThrowsAsync(new InvalidOperationException("Snupkg not found"));

                var service = new SymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger);

                // Act 
                var  result = await service.ValidateSymbolsAsync(Message, CancellationToken.None);

                // Assert 
                Assert.Equal(NuGetValidationResponse.Failed.Status, result.Status);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfNupkgNotFound()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ThrowsAsync(new FileNotFoundException("Nupkg not found"));

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                var service = new SymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger);

                // Act 
                var result = await service.ValidateSymbolsAsync(Message, CancellationToken.None);

                // Assert 
                Assert.Equal(NuGetValidationResponse.Failed.Status, result.Status);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfTheSnupkgNotSubsetOfNupkg()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), It.IsAny<string[]>())).Returns(new List<string>()
                { "foo.dll" });

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), ".pdb")).Returns(new List<string>()
                { "bar.pdb" });

                var service = new SymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger);

                // Act 
                var result = await service.ValidateSymbolsAsync(Message, CancellationToken.None);

                // Assert 
                Assert.Equal(NuGetValidationResponse.Failed.Status, result.Status);
                Assert.Single(result.Issues);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillSucceedOnCorrectMatch()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _zipService.Setup(s => s.ValidateZipAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), It.IsAny<string[]>())).Returns(new List<string>()
                { "foo.dll" });

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), ".pdb")).Returns(new List<string>()
                { "foo.pdb" });

                var service = new TestSymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger);

                // Act 
                var result = await service.ValidateSymbolsAsync(Message, CancellationToken.None);

                // Assert 
                Assert.Equal(NuGetValidationResponse.Succeeded.Status, result.Status);
                Assert.True(service.ValidateSymbolMatchingInvoked);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfSnupkgIsNotSafeForExtract()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(CreateZipSlipStream());
                var zipService = new ZipArchiveService(new Mock<ILogger<ZipArchiveService>>().Object);
                var service = new TestSymbolsValidatorService(_symbolsFileService.Object, zipService, _telemetryService.Object, _logger);

                // Act 
                var result = await service.ValidateSymbolsAsync(Message, CancellationToken.None);

                // Assert 
                Assert.Equal(NuGetValidationResponse.Failed.Status, result.Status);
                Assert.Single(result.Issues);
            }

            [Fact]
            public async Task ValidateSymbolsAsyncWillFailIfSnupkgDoesNotHavePDBs()
            {
                // Arrange
                _symbolsFileService.
                    Setup(sfs => sfs.DownloadNupkgFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _symbolsFileService.
                    Setup(sfs => sfs.DownloadSnupkgFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(new MemoryStream());

                _zipService.Setup(s => s.ValidateZipAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

                _zipService.Setup(s => s.ReadFilesFromZipStream(It.IsAny<Stream>(), It.IsAny<string[]>())).Returns(new List<string>());

                var service = new TestSymbolsValidatorService(_symbolsFileService.Object, _zipService.Object, _telemetryService.Object, _logger);

                // Act 
                var result = await service.ValidateSymbolsAsync(Message, CancellationToken.None);

                // Assert 
                Assert.Equal(NuGetValidationResponse.Failed.Status, result.Status);
                Assert.Single(result.Issues);
            }

            private Stream CreateZipSlipStream()
            {
                string text =
                               "<package xmlns = \"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\"> " +
                               "<metadata>" +
                               "<id>OneId</id> " +
                               "<version>1.0.0</version>" +
                               "<authors>xxx yyy</authors>" +
                               "<description>Test.</description>" +
                               "<language>en-US</language>" +
                               "</metadata>" +
                               "</package>";

                var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entryNuspec = archive.CreateEntry(@"foo.nuspec");
                    using (var entryNuspecStream = entryNuspec.Open())
                    using (var streamWriter = new StreamWriter(entryNuspecStream))
                    {
                        streamWriter.Write(text);
                    }
                    var entryEvil = archive.CreateEntry(@"../../evil.txt");
                    using (var entryEvilStream = entryEvil.Open())
                    using (var streamWriter = new StreamWriter(entryEvilStream))
                    {
                        streamWriter.Write("Evil stuff");
                    }
                }
                return memoryStream;
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
                List<string> orphanSymbols;
                var result = SymbolsValidatorService.SymbolsHaveMatchingPEFiles(symbols, pes, out orphanSymbols);

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
                List<string> orphanSymbols;
                var result = SymbolsValidatorService.SymbolsHaveMatchingPEFiles(symbols, pes, out orphanSymbols);

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
                List<string> orphanSymbols;
                Assert.Throws<ArgumentNullException>(()=> SymbolsValidatorService.SymbolsHaveMatchingPEFiles(null, pes, out orphanSymbols));
                Assert.Throws<ArgumentNullException>(() => SymbolsValidatorService.SymbolsHaveMatchingPEFiles(symbols, null, out orphanSymbols));
            }
        }

        public class TheIsPortableMethod
        {
            [Theory]
            [InlineData("BSJB", true)]
            [InlineData("CBSJB", false)]
            public void IsPortableValidation(string data, bool expectedResult)
            {
                // Arrange + Act
                bool result = false;
                using (MemoryStream memStream = new MemoryStream(Encoding.ASCII.GetBytes(data)))
                {
                    result = SymbolsValidatorService.IsPortable(memStream);
                }

                // Assert
                Assert.Equal(result, expectedResult);
            }
        }

        public class TheGetSymbolPathMethod
        {
            [Theory]
            [InlineData(@"C:\A\BB\CC.dll", @"C:\A\BB\CC.pdb")]
            [InlineData(@"C:\A\BB\cc.dll", @"C:\A\BB\cc.pdb")]
            [InlineData(@"C:\A\BB\CC.exe", @"C:\A\BB\CC.pdb")]
            public void IsGetSymbolPathValidation(string input, string expectedResult)
            {
                // Act 
                string result = SymbolsValidatorService.GetSymbolPath(input);

                // Assert
                Assert.Equal(result, expectedResult);
            }
        }

        public class FactBase
        {
            public Mock<ISymbolsFileService> _symbolsFileService;
            public Mock<ITelemetryService> _telemetryService;
            public ILogger<SymbolsValidatorService> _logger;
            public Mock<IZipArchiveService> _zipService;

            public FactBase(ITestOutputHelper output)
            {
                _symbolsFileService = new Mock<ISymbolsFileService>();
                _telemetryService = new Mock<ITelemetryService>();
                _logger = new LoggerFactory().AddXunit(output).CreateLogger<SymbolsValidatorService>();
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

            public override INuGetValidationResponse ValidateSymbolMatching(string targetDirectory, string packageId, string packageNormalizedVersion)
            {
                ValidateSymbolMatchingInvoked = true;
                return NuGetValidationResponse.Succeeded;
            }
        }
    }
}
