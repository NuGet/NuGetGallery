// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Jobs.Validation.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class SignatureValidatorFacts
    {
        public class ValidateAsync
        {
            private MemoryStream _packageStream;
            private readonly int _packageKey;
            private SignatureValidationMessage _message;
            private readonly CancellationToken _cancellationToken;
            private readonly Mock<IPackageSigningStateService> _packageSigningStateService;

            private readonly Mock<ISignatureFormatValidator> _formatValidator;
            private VerifySignaturesResult _minimalVerifyResult;
            private VerifySignaturesResult _fullVerifyResult;
            private VerifySignaturesResult _authorSignatureVerifyResult;
            private VerifySignaturesResult _repositorySignatureVerifyResult;
            private readonly Mock<ISignaturePartsExtractor> _signaturePartsExtractor;
            private readonly Mock<ICorePackageService> _corePackageService;
            private readonly ILogger<SignatureValidator> _logger;
            private readonly Mock<IProcessorPackageFileService> _packageFileService;
            private readonly Uri _nupkgUri;
            private readonly SignatureValidator _target;
            private readonly Mock<IOptionsSnapshot<ProcessSignatureConfiguration>> _optionsSnapshot;
            private readonly Mock<IOptionsSnapshot<SasDefinitionConfiguration>> _sasDefinitionConfigurationMock;
            private readonly ProcessSignatureConfiguration _configuration;
            private readonly SasDefinitionConfiguration _sasDefinitionConfiguration;
            private readonly Mock<ITelemetryService> _telemetryService;

            public ValidateAsync(ITestOutputHelper output)
            {
                _packageStream = TestResources.GetResourceStream(TestResources.UnsignedPackage);
                _packageKey = 42;
                _message = new SignatureValidationMessage(
                    "NuGet.Versioning",
                    "4.3.0",
                    new Uri("https://example/nuget.versioning.4.3.0.nupkg"),
                    new Guid("b777135f-1aac-4ec2-a3eb-1f64fe1880d5"));
                _cancellationToken = CancellationToken.None;

                _packageSigningStateService = new Mock<IPackageSigningStateService>();
                _formatValidator = new Mock<ISignatureFormatValidator>();

                _minimalVerifyResult = new VerifySignaturesResult(isValid: true, isSigned: true);
                _formatValidator
                    .Setup(x => x.ValidateMinimalAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _minimalVerifyResult);

                _fullVerifyResult = new VerifySignaturesResult(isValid: true, isSigned: true);
                _formatValidator
                    .Setup(x => x.ValidateAllSignaturesAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _fullVerifyResult);

                _authorSignatureVerifyResult = new VerifySignaturesResult(isValid: true, isSigned: true);
                _formatValidator
                    .Setup(x => x.ValidateAuthorSignatureAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _authorSignatureVerifyResult);

                _repositorySignatureVerifyResult = new VerifySignaturesResult(isValid: true, isSigned: true);
                _formatValidator
                    .Setup(x => x.ValidateRepositorySignatureAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _repositorySignatureVerifyResult);

                _signaturePartsExtractor = new Mock<ISignaturePartsExtractor>();
                _corePackageService = new Mock<ICorePackageService>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                _logger = loggerFactory.CreateLogger<SignatureValidator>();

                _packageFileService = new Mock<IProcessorPackageFileService>();
                _nupkgUri = new Uri("https://example-storage/TestProcessor/b777135f-1aac-4ec2-a3eb-1f64fe1880d5/nuget.versioning.4.3.0.nupkg");
                _packageFileService
                    .Setup(x => x.GetReadAndDeleteUriAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()))
                    .ReturnsAsync(() => _nupkgUri);

                _optionsSnapshot = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();
                _configuration = new ProcessSignatureConfiguration
                {
                    AllowedRepositorySigningCertificates = new List<string> { "fake-thumbprint" },
                    V3ServiceIndexUrl = TestResources.V3ServiceIndexUrl,
                    StripValidRepositorySignatures = false,
                };
                _optionsSnapshot.Setup(x => x.Value).Returns(() => _configuration);

                _sasDefinitionConfiguration = new SasDefinitionConfiguration();
                _sasDefinitionConfigurationMock = new Mock<IOptionsSnapshot<SasDefinitionConfiguration>>();
                _sasDefinitionConfigurationMock.Setup(x => x.Value).Returns(() => _sasDefinitionConfiguration);


                _telemetryService = new Mock<ITelemetryService>();

                _target = new SignatureValidator(
                    _packageSigningStateService.Object,
                    _formatValidator.Object,
                    _signaturePartsExtractor.Object,
                    _packageFileService.Object,
                    _corePackageService.Object,
                    _optionsSnapshot.Object,
                    _sasDefinitionConfigurationMock.Object,
                    _telemetryService.Object,
                    _logger);
            }

            private void Validate(
                SignatureValidatorResult result,
                ValidationStatus validationStatus,
                PackageSigningStatus packageSigningStatus,
                Uri nupkgUri = null,
                bool? shouldExtract = null)
            {
                Assert.Equal(validationStatus, result.State);
                Assert.Equal(nupkgUri, result.NupkgUri);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        _packageKey,
                        _message.PackageId,
                        _message.PackageVersion,
                        packageSigningStatus),
                    Times.Once);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageSigningStatus>()),
                    Times.Once);

                if ((shouldExtract.HasValue && shouldExtract.Value) ||
                    (validationStatus == ValidationStatus.Succeeded && packageSigningStatus == PackageSigningStatus.Valid))
                {
                    _signaturePartsExtractor.Verify(
                        x => x.ExtractAsync(_packageKey, It.Is<PrimarySignature>(y => y != null), It.IsAny<CancellationToken>()),
                        Times.Once);
                    _signaturePartsExtractor.Verify(
                        x => x.ExtractAsync(It.IsAny<int>(), It.IsAny<PrimarySignature>(), It.IsAny<CancellationToken>()),
                        Times.Once);
                }
                else
                {
                    _signaturePartsExtractor.Verify(
                        x => x.ExtractAsync(It.IsAny<int>(), It.IsAny<PrimarySignature>(), It.IsAny<CancellationToken>()),
                        Times.Never);
                }

                _corePackageService.VerifyAll();
            }

            [Fact]
            public async Task RejectsZip64Packages()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.Zip64Package);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                Assert.Equal(ValidationIssueCode.PackageIsZip64, issue.IssueCode);
            }

            [Fact]
            public async Task RejectsUnsignedPackagesWhenSigningIsRequired()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.UnsignedPackage);
                TestUtility.RequireSignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
                _message = new SignatureValidationMessage(
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion,
                    new Uri($"https://unit.test/{TestResources.UnsignedPackage.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                Assert.Equal(ValidationIssueCode.PackageIsNotSigned, issue.IssueCode);
            }

            [Fact]
            public async Task RejectsRepositorySignedPackagesWhenAuthorSigningIsRequired()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.RepoSignedPackageLeaf1);
                TestUtility.RequireSignedPackage(_corePackageService, TestResources.RepoSignedPackageLeafId, TestResources.RepoSignedPackageLeaf1Version);
                _message = new SignatureValidationMessage(
                    TestResources.RepoSignedPackageLeafId,
                    TestResources.RepoSignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.UnsignedPackage.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                Assert.Equal(ValidationIssueCode.PackageIsNotSigned, issue.IssueCode);
            }

            [Fact]
            public async Task RejectsUnsignedPackagesWhenRepositorySigningIsRequired()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.UnsignedPackage);
                TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
                _message = new SignatureValidationMessage(
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion,
                    new Uri($"https://unit.test/{TestResources.UnsignedPackage.ToLowerInvariant()}"),
                    Guid.NewGuid(),
                    requireRepositorySignature: true);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Unsigned);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task AcceptsSignedPackagesWithKnownCertificates()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    TestResources.Leaf1Thumbprint);
                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task RejectsSignedPackagesWithFailedMinimalVerifyResult()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                _minimalVerifyResult = new VerifySignaturesResult(isValid: false, isSigned: true);
                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Empty(result.Issues);
                _formatValidator.Verify(
                    x => x.ValidateAllSignaturesAsync(It.IsAny<ISignedPackageReader>(), false, It.IsAny<CancellationToken>()),
                    Times.Never);
            }

            [Fact]
            public async Task RejectsPackagesWithMimimalVerificationErrors()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                _minimalVerifyResult = new VerifySignaturesResult(
                    isValid: false,
                    isSigned: true,
                    results: new[]
                    {
                        new InvalidSignaturePackageVerificationResult(
                            SignatureVerificationStatus.Suspect,
                            new[]
                            {
                                SignatureLog.Issue(
                                    fatal: true,
                                    code: NuGetLogCode.NU3000,
                                    message: "The package signature is invalid."),
                            })
                    });
                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Single(result.Issues);
                var issue = Assert.IsType<ClientSigningVerificationFailure>(result.Issues[0]);
                Assert.Equal("NU3000", issue.ClientCode);
                Assert.Equal("The package signature is invalid.", issue.ClientMessage);
            }

            [Fact]
            public async Task RejectsSignedPackagesWithKnownCertificatesButFailedFullVerifyResult()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    TestResources.Leaf1Thumbprint);
                _fullVerifyResult = new VerifySignaturesResult(isValid: false, isSigned: true);
                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task RejectsPackagesWithFullVerificationErrors()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    TestResources.Leaf1Thumbprint);
                _fullVerifyResult = new VerifySignaturesResult(
                    isValid: false,
                    isSigned: true,
                    results: new[]
                    {
                        new InvalidSignaturePackageVerificationResult(
                            SignatureVerificationStatus.Suspect,
                            new[]
                            {
                                SignatureLog.Issue(
                                    fatal: true,
                                    code: NuGetLogCode.NU3008,
                                    message: "The package integrity check failed."),
                                SignatureLog.Issue(
                                    fatal: false,
                                    code: NuGetLogCode.NU3016,
                                    message: "The package hash uses an unsupported hash algorithm."),
                                SignatureLog.Issue(
                                    fatal: true,
                                    code: NuGetLogCode.NU3000,
                                    message: "Some other thing happened."),
                            })
                    });
                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Equal(2, result.Issues.Count);
                var issue1 = Assert.IsType<ClientSigningVerificationFailure>(result.Issues[0]);
                Assert.Equal("NU3008", issue1.ClientCode);
                Assert.Equal("The package integrity check failed.", issue1.ClientMessage);
                var issue2 = Assert.IsType<ClientSigningVerificationFailure>(result.Issues[1]);
                Assert.Equal("NU3000", issue2.ClientCode);
                Assert.Equal("Some other thing happened.", issue2.ClientMessage);
            }

            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.FailedValidation)]
            [InlineData(PackageStatus.Validating)]
            public async Task RejectsSignedPackagesWithUnknownCertificates(PackageStatus packageStatus)
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    TestResources.Leaf2Thumbprint,
                    packageStatus);
                _message = new SignatureValidationMessage(
                   TestResources.SignedPackageLeafId,
                   TestResources.SignedPackageLeaf1Version,
                   new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                   Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Single(result.Issues);
                var issue = Assert.IsType<UnauthorizedCertificateFailure>(result.Issues[0]);
                Assert.Equal(ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate, issue.IssueCode);
                Assert.Equal(TestResources.Leaf2Sha1Thumbprint, issue.Sha1Thumbprint);
            }

            [Fact]
            public async Task AcceptsSignedPackagesWithUnknownCertificatesOnRevalidation()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    TestResources.Leaf2Thumbprint,
                    PackageStatus.Available);

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task AcceptsRepositorySignedPackage()
            {
                // Arrange
                _configuration.AllowedRepositorySigningCertificates = new List<string> { TestResources.Leaf1Thumbprint };
                _packageStream = TestResources.GetResourceStream(TestResources.RepoSignedPackageLeaf1);

                TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.RepoSignedPackageLeafId, TestResources.RepoSignedPackageLeaf1Version);

                _message = new SignatureValidationMessage(
                    TestResources.RepoSignedPackageLeafId,
                    TestResources.RepoSignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.RepoSignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task WhenRepositorySigningIsRequired_FailsValidationOfSignedPackagesWithNoRepositorySignature()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    TestResources.Leaf1Thumbprint);
                _message = new SignatureValidationMessage(
                   TestResources.SignedPackageLeafId,
                   TestResources.SignedPackageLeaf1Version,
                   new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                   Guid.NewGuid(),
                   requireRepositorySignature: true);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Valid, shouldExtract: true);
                Assert.Empty(result.Issues);
            }

            [Theory]
            [InlineData(
                TestResources.RepoSignedPackageLeaf1,
                TestResources.RepoSignedPackageLeafId,
                TestResources.RepoSignedPackageLeaf1Version,
                PackageSigningStatus.Unsigned,
                false)]
            [InlineData(
                TestResources.AuthorAndRepoSignedPackageLeaf1,
                TestResources.AuthorAndRepoSignedPackageLeafId,
                TestResources.AuthorAndRepoSignedPackageLeaf1Version,
                PackageSigningStatus.Valid,
                true)]
            public async Task WhenRepositorySigningIsRequired_FailsValidationOfPackageWhoseRepositorySignatureIsStripped(
                string resourceName,
                string packageId,
                string packageVersion,
                PackageSigningStatus signingStatus,
                bool allowSignedPackage)
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(resourceName);
                if (allowSignedPackage)
                {
                    TestUtility.RequireSignedPackage(_corePackageService, packageId, packageVersion, TestResources.Leaf1Thumbprint);
                }
                else
                {
                    TestUtility.RequireUnsignedPackage(_corePackageService, packageId, packageVersion);
                }
                _message = new SignatureValidationMessage(
                   packageId,
                   packageVersion,
                   new Uri($"https://unit.test/{TestResources.RepoSignedPackageLeafId.ToLowerInvariant()}"),
                   Guid.NewGuid(),
                   requireRepositorySignature: true);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, signingStatus, shouldExtract: signingStatus == PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task DoesNotUploadPackageWhenValidationFailed()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.AuthorAndRepoSignedPackageLeaf1);
                TestUtility.RequireUnsignedPackage(_corePackageService, _message.PackageId, _message.PackageVersion);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                _packageFileService.Verify(
                    x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Stream>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(
                TestResources.RepoSignedPackageLeaf1,
                TestResources.RepoSignedPackageLeafId,
                TestResources.RepoSignedPackageLeaf1Version,
                PackageSigningStatus.Unsigned,
                false)]
            [InlineData(
                TestResources.AuthorAndRepoSignedPackageLeaf1,
                TestResources.AuthorAndRepoSignedPackageLeafId,
                TestResources.AuthorAndRepoSignedPackageLeaf1Version,
                PackageSigningStatus.Valid,
                true)]
            public async Task StripsAndAcceptsPackagesWithRepositorySignatures(
                string resourceName,
                string packageId,
                string packageVersion,
                PackageSigningStatus signingStatus,
                bool allowSignedPackage)
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(resourceName);
                if (allowSignedPackage)
                {
                    TestUtility.RequireSignedPackage(_corePackageService, packageId, packageVersion, TestResources.Leaf1Thumbprint);
                }
                else
                {
                    TestUtility.RequireUnsignedPackage(_corePackageService, packageId, packageVersion);
                }
                _message = new SignatureValidationMessage(
                   packageId,
                   packageVersion,
                   new Uri($"https://unit.test/{resourceName.ToLowerInvariant()}"),
                   Guid.NewGuid());

                Stream uploadedStream = null;
                _packageFileService
                    .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback<string, string, Guid, Stream>((_, __, ___, s) => uploadedStream = s);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, signingStatus, _nupkgUri);
                Assert.Empty(result.Issues);
                _packageFileService.Verify(
                    x => x.SaveAsync(_message.PackageId, _message.PackageVersion, _message.ValidationId, It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.GetReadAndDeleteUriAsync(_message.PackageId, _message.PackageVersion, _message.ValidationId, _sasDefinitionConfiguration.SignatureValidatorSasDefinition),
                    Times.Once);
                Assert.IsType<FileStream>(uploadedStream);
                Assert.Throws<ObjectDisposedException>(() => uploadedStream.Length);
                if (signingStatus == PackageSigningStatus.Valid)
                {
                    _formatValidator.Verify(
                        x => x.ValidateAllSignaturesAsync(It.IsAny<ISignedPackageReader>(), false, It.IsAny<CancellationToken>()),
                        Times.Once);
                }
                else
                {
                    _formatValidator.Verify(
                        x => x.ValidateAllSignaturesAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                        Times.Never);
                }
            }

            [Theory]
            [InlineData(
                TestResources.RepoSignedPackageLeaf1,
                TestResources.RepoSignedPackageLeafId,
                TestResources.RepoSignedPackageLeaf1Version,
                PackageSigningStatus.Unsigned)]
            [InlineData(
                TestResources.AuthorAndRepoSignedPackageLeaf1,
                TestResources.AuthorAndRepoSignedPackageLeafId,
                TestResources.AuthorAndRepoSignedPackageLeaf1Version,
                PackageSigningStatus.Valid)]
            public async Task WhenStripsValidRepositorySignature_StripsAndAcceptsRepositorySignatureWhenRepositorySignatureIsNotRequired(
                string resourceName,
                string packageId,
                string packageVersion,
                PackageSigningStatus expectedSigningStatus)
            {
                // Arrange
                _configuration.StripValidRepositorySignatures = true;
                _configuration.AllowedRepositorySigningCertificates = new List<string> { TestResources.Leaf1Thumbprint, TestResources.Leaf2Thumbprint };

                _packageStream = TestResources.GetResourceStream(resourceName);

                if (resourceName == TestResources.RepoSignedPackageLeaf1)
                {
                    TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.RepoSignedPackageLeafId, TestResources.RepoSignedPackageLeaf1Version);
                }

                if (resourceName == TestResources.AuthorAndRepoSignedPackageLeaf1)
                {
                    TestUtility.RequireSignedPackage(_corePackageService, TestResources.AuthorAndRepoSignedPackageLeafId, TestResources.AuthorAndRepoSignedPackageLeaf1Version, TestResources.Leaf1Thumbprint);
                }

                _message = new SignatureValidationMessage(
                    packageId,
                    packageVersion,
                    new Uri($"https://unit.test/{resourceName.ToLowerInvariant()}"),
                    Guid.NewGuid(),
                    requireRepositorySignature: false);

                Stream uploadedStream = null;
                _packageFileService
                    .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback<string, string, Guid, Stream>((_, __, ___, s) => uploadedStream = s);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, expectedSigningStatus, _nupkgUri);
                Assert.Empty(result.Issues);
                _packageFileService.Verify(
                    x => x.SaveAsync(_message.PackageId, _message.PackageVersion, _message.ValidationId, It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.GetReadAndDeleteUriAsync(_message.PackageId, _message.PackageVersion, _message.ValidationId, _sasDefinitionConfiguration.SignatureValidatorSasDefinition),
                    Times.Once);
                Assert.IsType<FileStream>(uploadedStream);
                Assert.Throws<ObjectDisposedException>(() => uploadedStream.Length);
            }

            [Theory]
            [InlineData(
                TestResources.RepoSignedPackageLeaf1,
                TestResources.RepoSignedPackageLeafId,
                TestResources.RepoSignedPackageLeaf1Version,
                PackageSigningStatus.Valid)]
            [InlineData(
                TestResources.AuthorAndRepoSignedPackageLeaf1,
                TestResources.AuthorAndRepoSignedPackageLeafId,
                TestResources.AuthorAndRepoSignedPackageLeaf1Version,
                PackageSigningStatus.Valid)]
            public async Task WhenStripsValidRepositorySignature_AcceptsRepositorySignatureWhenRepositorySignatureIsRequired(
                string resourceName,
                string packageId,
                string packageVersion,
                PackageSigningStatus expectedSigningStatus)
            {
                // Arrange
                _configuration.StripValidRepositorySignatures = true;
                _configuration.AllowedRepositorySigningCertificates = new List<string> { TestResources.Leaf1Thumbprint, TestResources.Leaf2Thumbprint };

                _packageStream = TestResources.GetResourceStream(resourceName);

                if (resourceName == TestResources.RepoSignedPackageLeaf1)
                {
                    TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.RepoSignedPackageLeafId, TestResources.RepoSignedPackageLeaf1Version);
                }

                if (resourceName == TestResources.AuthorAndRepoSignedPackageLeaf1)
                {
                    TestUtility.RequireSignedPackage(_corePackageService, TestResources.AuthorAndRepoSignedPackageLeafId, TestResources.AuthorAndRepoSignedPackageLeaf1Version, TestResources.Leaf1Thumbprint);
                }

                _message = new SignatureValidationMessage(
                    packageId,
                    packageVersion,
                    new Uri($"https://unit.test/{resourceName.ToLowerInvariant()}"),
                    Guid.NewGuid(),
                    requireRepositorySignature: true);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, expectedSigningStatus);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task StripsAndRejectsPackagesWithRepositorySignatureWhenPackageMustBeAuthorSigned()
            {
                _packageStream = TestResources.GetResourceStream(TestResources.RepoSignedPackageLeaf1);
                TestUtility.RequireSignedPackage(_corePackageService, TestResources.RepoSignedPackageLeafId, TestResources.RepoSignedPackageLeaf1Version);
                _message = new SignatureValidationMessage(
                   TestResources.RepoSignedPackageLeafId,
                   TestResources.RepoSignedPackageLeaf1Version,
                   new Uri($"https://unit.test/{TestResources.RepoSignedPackageLeaf1.ToLowerInvariant()}"),
                   Guid.NewGuid());

                Stream uploadedStream = null;
                _packageFileService
                    .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback<string, string, Guid, Stream>((_, __, ___, s) => uploadedStream = s);

                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Single(result.Issues);
                var issue = Assert.IsType<NoDataValidationIssue>(result.Issues[0]);
                Assert.Equal(ValidationIssueCode.PackageIsNotSigned, issue.IssueCode);
            }

            [Fact]
            public async Task StripsAndRejectsPackagesWithRepositorySignatureWhenPackageIsAuthorSignedWithUnknownCertificate()
            {
                _packageStream = TestResources.GetResourceStream(TestResources.AuthorAndRepoSignedPackageLeaf1);
                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.AuthorAndRepoSignedPackageLeafId,
                    TestResources.AuthorAndRepoSignedPackageLeaf1Version,
                    TestResources.Leaf2Thumbprint);
                _message = new SignatureValidationMessage(
                   TestResources.AuthorAndRepoSignedPackageLeafId,
                   TestResources.AuthorAndRepoSignedPackageLeaf1Version,
                   new Uri($"https://unit.test/{TestResources.AuthorAndRepoSignedPackageLeaf1.ToLowerInvariant()}"),
                   Guid.NewGuid());

                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                Assert.Single(result.Issues);
                var issue = Assert.IsType<UnauthorizedCertificateFailure>(result.Issues[0]);
                Assert.Equal(ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate, issue.IssueCode);
                Assert.Equal(TestResources.Leaf2Sha1Thumbprint, issue.Sha1Thumbprint);
            }

            [Fact]
            public async Task WhenPackageSupportsButDoesNotRequireSigning_AcceptsUnsignedPackages()
            {
                // Arrange
                var user1 = new User
                {
                    Key = 1
                };
                var user2 = new User
                {
                    Key = 2
                };
                var packageRegistration = new PackageRegistration
                {
                    Key = 3,
                    Id = TestResources.UnsignedPackageId
                };
                var package = new Package
                {
                    PackageStatusKey = PackageStatus.Validating,
                };
                var certificate = new Certificate
                {
                    Key = 4,
                    Thumbprint = TestResources.Leaf1Thumbprint
                };
                var userCertificate = new UserCertificate
                {
                    Key = 5,
                    CertificateKey = certificate.Key,
                    Certificate = certificate,
                    UserKey = user1.Key,
                    User = user1
                };

                user1.UserCertificates.Add(userCertificate);
                certificate.UserCertificates.Add(userCertificate);

                packageRegistration.Owners.Add(user1);
                packageRegistration.Owners.Add(user2);

                _message = new SignatureValidationMessage(
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion,
                    new Uri($"https://unit.test/{TestResources.UnsignedPackage.ToLowerInvariant()}"),
                    Guid.NewGuid());
                _packageStream = TestResources.GetResourceStream(TestResources.UnsignedPackage);
                _corePackageService
                    .Setup(x => x.FindPackageRegistrationById(_message.PackageId))
                    .Returns(packageRegistration);
                _corePackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(_message.PackageId, _message.PackageVersion))
                    .Returns(package);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task WhenPackageSupportsButDoesNotRequireSigning_AcceptsPackagesSignedWithKnownCertificates()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                TestUtility.RequireSignedPackage(_corePackageService, TestResources.SignedPackageLeafId, TestResources.SignedPackageLeaf1Version, TestResources.Leaf1Thumbprint);
                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public async Task WhenPackageRequiresUnsignedPackages_AcceptsUnsignedPackages()
            {
                // Arrange
                _packageStream = TestResources.GetResourceStream(TestResources.UnsignedPackage);
                TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
                _message = new SignatureValidationMessage(
                     TestResources.UnsignedPackageId,
                     TestResources.UnsignedPackageVersion,
                     new Uri($"https://unit.test/{TestResources.UnsignedPackage.ToLowerInvariant()}"),
                     Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
                Assert.Empty(result.Issues);
            }
        }
    }
}