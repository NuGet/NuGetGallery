// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignatureValidatorIntegrationTests : IDisposable
    {
        private readonly Mock<IPackageSigningStateService> _packageSigningStateService;
        private readonly Mock<ISignaturePartsExtractor> _signaturePartsExtractor;
        private readonly Mock<IEntityRepository<Certificate>> _certificates;
        private readonly IPackageSignatureVerifier _packageSignatureVerifier;
        private readonly ILogger<SignatureValidator> _logger;
        private PackageArchiveReader _package;
        private readonly ValidatorStatus _validation;
        private readonly SignatureValidationMessage _message;
        private readonly CancellationToken _token;
        private readonly SignatureValidator _target;

        public SignatureValidatorIntegrationTests(ITestOutputHelper output)
        {
            // These dependencies have their own dependencies on the database or blob storage, which don't have good
            // integration test infrastructure in the jobs yet. Therefore, we'll mock them for now.
            _packageSigningStateService = new Mock<IPackageSigningStateService>();

            _signaturePartsExtractor = new Mock<ISignaturePartsExtractor>();

            _certificates = new Mock<IEntityRepository<Certificate>>();

            // These dependencies are concrete.
            _packageSignatureVerifier = PackageSignatureVerifierFactory.Create();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddXunit(output);
            _logger = loggerFactory.CreateLogger<SignatureValidator>();

            // Initialize data.
            _validation = new ValidatorStatus
            {
                PackageKey = 23,
                ValidationId = new Guid("8eb5affc-2d0e-4315-9b79-5a194d39ebd1"),
            };
            _message = new SignatureValidationMessage(
                "SomePackageId",
                "1.2.3",
                new Uri("https://example/validation/somepackageid.1.2.3.nupkg"),
                _validation.ValidationId);
            _token = CancellationToken.None;

            // Initialize the subject of testing.
            _target = new SignatureValidator(
                _packageSigningStateService.Object,
                _packageSignatureVerifier,
                _signaturePartsExtractor.Object,
                _certificates.Object,
                _logger);
        }

        [Theory]
        [InlineData(TestResources.SignedPackageLeaf1, TestResources.Leaf1Thumbprint)]
        [InlineData(TestResources.SignedPackageLeaf2, TestResources.Leaf2Thumbprint)]
        public async Task SuccessfullyValidatesLeaves(string resourceName, string thumbprint)
        {
            // Arrange
            _package = TestResources.LoadPackage(resourceName);
            AllowCertificateThumbprint(thumbprint);

            // Act
            await _target.ValidateAsync(
                _package,
                _validation,
                _message,
                _token);

            // Assert
            VerifyResult(ValidationStatus.Succeeded, PackageSigningStatus.Valid);
        }

        [Fact]
        public async Task RejectsPackageWithAddedFile()
        {
            // Arrange
            AllowCertificateThumbprint(TestResources.Leaf1Thumbprint);
            var packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.CreateEntry("new-file.txt").Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.WriteLine("These contents were added after the package was signed.");
                }

                _package = new PackageArchiveReader(packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            await _target.ValidateAsync(
                _package,
                _validation,
                _message,
                _token);

            // Assert
            VerifyResult(ValidationStatus.Failed, PackageSigningStatus.Invalid);
        }

        [Fact]
        public async Task RejectsPackageWithModifiedFile()
        {
            // Arrange
            AllowCertificateThumbprint(TestResources.Leaf1Thumbprint);
            var packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry("TestSigned.leaf-1.nuspec").Open())
                {
                    entryStream.Seek(0, SeekOrigin.End);
                    var extraBytes = Encoding.ASCII.GetBytes(Environment.NewLine);
                    entryStream.Write(extraBytes, 0, extraBytes.Length);
                }

                _package = new PackageArchiveReader(packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            await _target.ValidateAsync(
                _package,
                _validation,
                _message,
                _token);

            // Assert
            VerifyResult(ValidationStatus.Failed, PackageSigningStatus.Invalid);
        }

        private void AllowCertificateThumbprint(string thumbprint)
        {
            _certificates
                .Setup(x => x.GetAll())
                .Returns(new[] { new Certificate { Thumbprint = thumbprint } }.AsQueryable());
        }

        private void VerifyResult(ValidationStatus state, PackageSigningStatus status)
        {
            Assert.Equal(state, _validation.State);
            _packageSigningStateService.Verify(
                x => x.SetPackageSigningState(
                    _validation.PackageKey,
                    _message.PackageId,
                    _message.PackageVersion,
                    status),
                Times.Once);
        }

        public void Dispose()
        {
            _package?.Dispose();
        }
    }
}
