// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
using NuGet.Services.Validation.Issues;
using NuGetGallery;
using Xunit;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignatureValidatorFacts
    {
        public class ValidateAsync
        {
            private readonly Mock<ISignedPackageReader> _packageMock;
            private readonly int _packageKey;
            private readonly SignatureValidationMessage _message;
            private readonly CancellationToken _cancellationToken;
            private readonly Mock<IPackageSigningStateService> _packageSigningStateService;
            private VerifySignaturesResult _verifyResult;
            private readonly Mock<IPackageSignatureVerifier> _packageSignatureVerifier;
            private readonly Mock<ISignaturePartsExtractor> _signaturePartsExtractor;
            private readonly Mock<IEntityRepository<Certificate>> _certificates;
            private readonly Mock<ILogger<SignatureValidator>> _logger;
            private readonly SignatureValidator _target;

            public ValidateAsync()
            {
                _packageMock = new Mock<ISignedPackageReader>();
                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                _packageKey = 42;
                _message = new SignatureValidationMessage(
                    "NuGet.Versioning",
                    "4.3.0",
                    new Uri("https://example/nuget.versioning.4.3.0.nupkg"),
                    new Guid("b777135f-1aac-4ec2-a3eb-1f64fe1880d5"));
                _cancellationToken = CancellationToken.None;

                _packageSigningStateService = new Mock<IPackageSigningStateService>();

                _verifyResult = new VerifySignaturesResult(true);
                _packageSignatureVerifier = new Mock<IPackageSignatureVerifier>();
                _packageSignatureVerifier
                    .Setup(x => x.VerifySignaturesAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _verifyResult);

                _signaturePartsExtractor = new Mock<ISignaturePartsExtractor>();
                _certificates = new Mock<IEntityRepository<Certificate>>();
                _logger = new Mock<ILogger<SignatureValidator>>();

                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(Enumerable.Empty<Certificate>().AsQueryable());

                _target = new SignatureValidator(
                    _packageSigningStateService.Object,
                    _packageSignatureVerifier.Object,
                    _signaturePartsExtractor.Object,
                    _certificates.Object,
                    _logger.Object);
            }

            private void Validate(SignatureValidatorResult result, ValidationStatus validationStatus, PackageSigningStatus packageSigningStatus)
            {
                Assert.Equal(validationStatus, result.State);
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

                if (validationStatus == ValidationStatus.Succeeded
                    && packageSigningStatus == PackageSigningStatus.Valid)
                {
                    _signaturePartsExtractor.Verify(
                        x => x.ExtractAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<CancellationToken>()),
                        Times.Once);
                }
                else
                {
                    _signaturePartsExtractor.Verify(
                        x => x.ExtractAsync(It.IsAny<ISignedPackageReader>(), It.IsAny<CancellationToken>()),
                        Times.Never);
                }
            }

            [Fact]
            public async Task AcceptsSignedPackagesWithKnownCertificates()
            {
                // Arrange
                await ConfigureKnownSignedPackage(
                    TestResources.SignedPackageLeaf1Reader,
                    TestResources.Leaf1Thumbprint);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageMock.Object,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            }

            [Fact]
            public async Task RejectsSignedPackagesWithKnownCertificatesButFailedVerifyResult()
            {
                // Arrange
                await ConfigureKnownSignedPackage(
                    TestResources.SignedPackageLeaf1Reader,
                    TestResources.Leaf1Thumbprint);

                _verifyResult = new VerifySignaturesResult(valid: false);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageMock.Object,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            }

            [Fact]
            public async Task RejectsPackagesWithVerificationErrors()
            {
                // Arrange
                await ConfigureKnownSignedPackage(
                    TestResources.SignedPackageLeaf1Reader,
                    TestResources.Leaf1Thumbprint);

                _verifyResult = new VerifySignaturesResult(
                    valid: false,
                    results: new[]
                    {
                        new InvalidSignaturePackageVerificationResult(
                            SignatureVerificationStatus.Invalid,
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

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageMock.Object,
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

            [Fact]
            public async Task RejectsSignedPackagesWithUnknownCertificates()
            {
                // Arrange
                var signature = await TestResources.SignedPackageLeaf1Reader.GetSignatureAsync(CancellationToken.None);

                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _packageMock
                    .Setup(x => x.GetSignatureAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(signature);
                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(new[] { new Certificate { Thumbprint = TestResources.Leaf2Thumbprint } }.AsQueryable());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageMock.Object,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                Assert.IsType<PackageIsSigned>(issue);
            }

            [Fact]
            public async Task AcceptsUnsignedPackages()
            {
                // Arrange
                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageMock.Object,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
                Assert.Empty(result.Issues);
            }

            private async Task ConfigureKnownSignedPackage(ISignedPackageReader package, string thumbprint)
            {
                var signature = await package.GetSignatureAsync(CancellationToken.None);

                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _packageMock
                    .Setup(x => x.GetSignatureAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(signature);
                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(new[] { new Certificate { Thumbprint = thumbprint } }.AsQueryable());
            }
        }
    }
}
