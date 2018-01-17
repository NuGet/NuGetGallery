// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
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
        private readonly IPackageSignatureVerifier _minimalPackageSignatureVerifier;
        private readonly IPackageSignatureVerifier _fullPackageSignatureVerifier;
        private readonly ILogger<SignatureValidator> _logger;
        private readonly int _packageKey;
        private SignedPackageArchive _package;
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
            _minimalPackageSignatureVerifier = PackageSignatureVerifierFactory.CreateMinimal();
            _fullPackageSignatureVerifier = PackageSignatureVerifierFactory.CreateFull();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddXunit(output);
            _logger = loggerFactory.CreateLogger<SignatureValidator>();

            // Initialize data.
            _packageKey = 23;
            _message = new SignatureValidationMessage(
                "SomePackageId",
                "1.2.3",
                new Uri("https://example/validation/somepackageid.1.2.3.nupkg"),
                new Guid("8eb5affc-2d0e-4315-9b79-5a194d39ebd1"));
            _token = CancellationToken.None;

            // Initialize the subject of testing.
            _target = new SignatureValidator(
                _packageSigningStateService.Object,
                _minimalPackageSignatureVerifier,
                _fullPackageSignatureVerifier,
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
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
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

                _package = new SignedPackageArchive(packageStream, packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
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

                _package = new SignedPackageArchive(packageStream, packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
        }

        [Fact]
        public async Task RejectsInvalidSignedCms()
        {
            // Arrange
            AllowCertificateThumbprint(TestResources.Leaf1Thumbprint);
            var packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry(".signature.p7s").Open())
                {
                    entryStream.Position = 0;
                    entryStream.SetLength(0);
                    var bytes = Encoding.ASCII.GetBytes("This is not a valid signed CMS.");
                    entryStream.Write(bytes, 0, bytes.Length);
                }

                _package = new SignedPackageArchive(packageStream, packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
        }

        [Fact]
        public async Task RejectsNonAuthorSignature()
        {
            // Arrange
            var packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry(".signature.p7s").Open())
                using (var certificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                {
                    AllowCertificateThumbprint(certificate.ComputeSHA256Thumbprint());

                    var content = new SignatureContent(
                        SigningSpecifications.V1,
                        HashAlgorithmName.SHA256,
                        hashValue: "hash");
                    var contentInfo = new ContentInfo(content.GetBytes());
                    var signedCms = new SignedCms(contentInfo);
                    var cmsSigner = new CmsSigner(certificate);

                    signedCms.ComputeSignature(cmsSigner);
                    var bytes = signedCms.Encode();

                    entryStream.Position = 0;
                    entryStream.SetLength(0);
                    entryStream.Write(bytes, 0, bytes.Length);
                }

                _package = new SignedPackageArchive(packageStream, packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.OnlyAuthorSignaturesSupported, issue.IssueCode);
        }

        [Fact]
        public async Task RejectsZip64Packages()
        {
            // Arrange
            _package = TestResources.LoadPackage(TestResources.Zip64Package);

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
        }

        private void AllowCertificateThumbprint(string thumbprint)
        {
            _certificates
                .Setup(x => x.GetAll())
                .Returns(new[] { new Certificate { Thumbprint = thumbprint } }.AsQueryable());
        }

        private void VerifyPackageSigningStatus(SignatureValidatorResult result, ValidationStatus validationStatus, PackageSigningStatus packageSigningStatus)
        {
            Assert.Equal(validationStatus, result.State);
            _packageSigningStateService.Verify(
                x => x.SetPackageSigningState(
                    _packageKey,
                    _message.PackageId,
                    _message.PackageVersion,
                    packageSigningStatus),
                Times.Once);
        }

        public void Dispose()
        {
            _package?.Dispose();
        }
    }
}
