// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;
using Test.Utility.Signing;
using Xunit;
using Xunit.Abstractions;
using NuGetHashAlgorithmName = NuGet.Common.HashAlgorithmName;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    [Collection(CertificateIntegrationTestCollection.Name)]
    public class SignatureValidatorIntegrationTests : IDisposable
    {
        private readonly CertificateIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly Mock<IPackageSigningStateService> _packageSigningStateService;
        private readonly Mock<ISignaturePartsExtractor> _signaturePartsExtractor;
        private readonly Mock<IEntityRepository<Certificate>> _certificates;
        private readonly List<string> _trustedThumbprints;
        private readonly IPackageSignatureVerifier _minimalPackageSignatureVerifier;
        private readonly IPackageSignatureVerifier _fullPackageSignatureVerifier;
        private readonly ILogger<SignatureValidator> _logger;
        private readonly int _packageKey;
        private SignedPackageArchive _package;
        private readonly SignatureValidationMessage _message;
        private readonly CancellationToken _token;
        private readonly SignatureValidator _target;

        public SignatureValidatorIntegrationTests(CertificateIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));

            // These dependencies have their own dependencies on the database or blob storage, which don't have good
            // integration test infrastructure in the jobs yet. Therefore, we'll mock them for now.
            _packageSigningStateService = new Mock<IPackageSigningStateService>();

            _signaturePartsExtractor = new Mock<ISignaturePartsExtractor>();

            _certificates = new Mock<IEntityRepository<Certificate>>();
            _trustedThumbprints = new List<string>();
            _certificates
                .Setup(x => x.GetAll())
                .Returns(() => _trustedThumbprints.Select(x => new Certificate { Thumbprint = x }).AsQueryable());

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

        public async Task<SignedPackageArchive> GetSignedPackage1Async()
        {
            AllowCertificateThumbprint(_fixture.LeafCertificate1Thumbprint);
            return await _fixture.GetSignedPackage1Async(_output);
        }

        public async Task<MemoryStream> GetSignedPackageStream1Async()
        {
            AllowCertificateThumbprint(_fixture.LeafCertificate1Thumbprint);
            return await _fixture.GetSignedPackageStream1Async(_output);
        }

        [Fact]
        public async Task AcceptsValidSignedPackage()
        {
            // Arrange
            _package = await GetSignedPackage1Async();

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public async Task RejectsPackageWithAddedFile()
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();

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
            VerifyNU3008(result);
        }

        [Fact]
        public async Task RejectsPackageWithModifiedFile()
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();

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
            VerifyNU3008(result);
        }

        [Fact]
        public async Task RejectsInvalidSignedCms()
        {
            // Arrange
            SetSignatureFileContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                Encoding.ASCII.GetBytes("This is not a valid signed CMS."));

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3003", typedIssue.ClientCode);
            Assert.Equal("The package signature is invalid.", typedIssue.ClientMessage);
        }

        [Fact]
        public async Task RejectsMultipleSignatures()
        {
            // Arrange
            SetSignatureContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                configuredSignedCms: signedCms =>
                {
                    using (var additionalCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowCertificateThumbprint(additionalCertificate.ComputeSHA256Thumbprint());
                        signedCms.ComputeSignature(new CmsSigner(additionalCertificate));
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3009", typedIssue.ClientCode);
            Assert.Equal("The package signature contains multiple primary signatures.", typedIssue.ClientMessage);
        }

        [Theory]
        [InlineData(SignatureType.Author)]
        [InlineData(SignatureType.Repository)]
        public async Task RejectsAuthorAndRepositoryCounterSignatures(SignatureType counterSignatureType)
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowCertificateThumbprint(counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);
                        cmsSigner.SignedAttributes.Add(AttributeUtility.CreateCommitmentTypeIndication(counterSignatureType));

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported, issue.IssueCode);
        }

        [Theory]
        [InlineData(new[] { SignatureType.Author, SignatureType.Repository })]
        [InlineData(new[] { SignatureType.Repository, SignatureType.Author })]
        public async Task RejectsMutuallyExclusiveCounterSignaturesCommitmentTypes(SignatureType[] counterSignatureTypes)
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowCertificateThumbprint(counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);
                        foreach (var type in counterSignatureTypes)
                        {
                            cmsSigner.SignedAttributes.Add(AttributeUtility.CreateCommitmentTypeIndication(type));
                        }

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3000", typedIssue.ClientCode);
            Assert.Equal("The commitment-type-indication attribute contains an invalid combination of values.", typedIssue.ClientMessage);
        }

        [Theory]
        [InlineData("MA0GCyqGSIb3DQEJEAYD")] // base64 of ASN.1 encoded "1.2.840.113549.1.9.16.6.3" OID.
        [InlineData(null)] // No commitment type.
        public async Task AllowsNonAuthorAndRepositoryCounterSignatures(string commitmentTypeOidBase64)
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowCertificateThumbprint(counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);

                        if (commitmentTypeOidBase64 != null)
                        {
                            var value = new AsnEncodedData(
                                Oids.CommitmentTypeIndication,
                                Convert.FromBase64String(commitmentTypeOidBase64));

                            var attribute = new CryptographicAttributeObject(
                                new Oid(Oids.CommitmentTypeIndication),
                                new AsnEncodedDataCollection(value));

                            cmsSigner.SignedAttributes.Add(attribute);
                        }
                        
                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);

            // This failure type indicates the counter signature validation passed.
            VerifyNU3008(result);
        }

        [Fact]
        public async Task RejectsInvalidSignatureContent()
        {
            // Arrange
            SetSignatureContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                "!!--:::FOO...");

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3000", typedIssue.ClientCode);
            Assert.Equal("The package signature content is invalid.", typedIssue.ClientMessage);
        }

        [Fact]
        public async Task RejectInvalidSignatureContentVersion()
        {
            // Arrange
            SetSignatureContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                "Version:2" + Environment.NewLine + Environment.NewLine + "2.16.840.1.101.3.4.2.1-Hash:hash");

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _package,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.OnlySignatureFormatVersion1Supported, issue.IssueCode);
        }

        [Fact]
        public async Task RejectsNonAuthorSignature()
        {
            // Arrange
            var content = new SignatureContent(
                SigningSpecifications.V1,
                NuGetHashAlgorithmName.SHA256,
                hashValue: "hash");
            SetSignatureContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                content.GetBytes());

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
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.PackageIsZip64, issue.IssueCode);
        }

        private void SetSignatureFileContent(Stream packageStream, byte[] fileContent)
        {
            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry(".signature.p7s").Open())
                {
                    entryStream.Position = 0;
                    entryStream.SetLength(0);
                    entryStream.Write(fileContent, 0, fileContent.Length);
                }

                _package = new SignedPackageArchive(packageStream, packageStream);
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }
        }

        private void ModifySignatureContent(Stream packageStream, Action<SignedCms> configuredSignedCms = null)
        {
            SignedCms signedCms;
            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry(".signature.p7s").Open())
                {
                    var signature = Signature.Load(entryStream);
                    signedCms = signature.SignedCms;
                }
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            configuredSignedCms(signedCms);

            SetSignatureFileContent(packageStream, signedCms.Encode());
        }

        private void SetSignatureContent(Stream packageStream, byte[] signatureContent = null, Action<SignedCms> configuredSignedCms = null)
        {
            if (signatureContent == null)
            {
                signatureContent = new SignatureContent(
                    SigningSpecifications.V1,
                    NuGetHashAlgorithmName.SHA256,
                    hashValue: "hash").GetBytes();
            }

            using (var certificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
            {
                AllowCertificateThumbprint(certificate.ComputeSHA256Thumbprint());

                var contentInfo = new ContentInfo(signatureContent);
                var signedCms = new SignedCms(contentInfo);

                signedCms.ComputeSignature(new CmsSigner(certificate));

                configuredSignedCms?.Invoke(signedCms);

                var fileContent = signedCms.Encode();

                SetSignatureFileContent(packageStream, fileContent);
            }
        }

        private void SetSignatureContent(Stream packageStream, string signatureContent)
        {
            SetSignatureContent(packageStream, signatureContent: Encoding.UTF8.GetBytes(signatureContent));
        }

        private void AllowCertificateThumbprint(string thumbprint)
        {
            _trustedThumbprints.Add(thumbprint);
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

        private static void VerifyNU3008(SignatureValidatorResult result)
        {
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3008", typedIssue.ClientCode);
            Assert.Equal("The package integrity check failed.", typedIssue.ClientMessage);
        }

        public void Dispose()
        {
            _package?.Dispose();
        }
    }
}
