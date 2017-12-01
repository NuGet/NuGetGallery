// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using NuGetGallery;
using Xunit;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignatureValidatorFacts
    {
        public class ValidateAsync
        {
            private readonly Mock<ISignedPackageReader> _packageMock;
            private readonly ValidatorStatus _validation;
            private readonly SignatureValidationMessage _message;
            private readonly CancellationToken _cancellationToken;
            private readonly Mock<IPackageSigningStateService> _packageSigningStateService;
            private readonly Mock<IEntityRepository<Certificate>> _certificates;
            private readonly Mock<ILogger<SignatureValidator>> _logger;
            private readonly SignatureValidator _target;

            public ValidateAsync()
            {
                _packageMock = new Mock<ISignedPackageReader>();
                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                _validation = new ValidatorStatus
                {
                    PackageKey = 42,
                    State = ValidationStatus.NotStarted,
                };
                _message = new SignatureValidationMessage(
                    "NuGet.Versioning",
                    "4.3.0",
                    new Uri("https://example/nuget.versioning.4.3.0.nupkg"),
                    new Guid("b777135f-1aac-4ec2-a3eb-1f64fe1880d5"));
                _cancellationToken = CancellationToken.None;

                _packageSigningStateService = new Mock<IPackageSigningStateService>();
                _certificates = new Mock<IEntityRepository<Certificate>>();
                _logger = new Mock<ILogger<SignatureValidator>>();

                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(Enumerable.Empty<Certificate>().AsQueryable());

                _target = new SignatureValidator(
                    _packageSigningStateService.Object,
                    _certificates.Object,
                    _logger.Object);
            }

            private void Validate(ValidationStatus validationStatus, PackageSigningStatus packageSigningStatus)
            {
                Assert.Equal(validationStatus, _validation.State);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        _validation.PackageKey,
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
            }

            [Fact]
            public async Task AcceptsSignedPackagesWithKnownCertificates()
            {
                // Arrange
                var signatures = await TestResources.SignedPackageLeaf1Reader.GetSignaturesAsync(CancellationToken.None);

                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _packageMock
                    .Setup(x => x.GetSignaturesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(signatures);
                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(new[] { new Certificate { Thumbprint = TestResources.Leaf1Thumbprint } }.AsQueryable());

                // Act
                await _target.ValidateAsync(
                    _packageMock.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            }

            [Fact]
            public async Task RejectsSignedPackagesWithUnknownCertificates()
            {
                // Arrange
                var signatures = await TestResources.SignedPackageLeaf1Reader.GetSignaturesAsync(CancellationToken.None);

                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _packageMock
                    .Setup(x => x.GetSignaturesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(signatures);
                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(new[] { new Certificate { Thumbprint = TestResources.Leaf2Thumbprint } }.AsQueryable());

                // Act
                await _target.ValidateAsync(
                    _packageMock.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(ValidationStatus.Failed, PackageSigningStatus.Invalid);
            }

            [Fact]
            public async Task RejectsSignedPackagesWithNoSignatures()
            {
                // Arrange
                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _packageMock
                    .Setup(x => x.GetSignaturesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Signature>());

                // Act
                await _target.ValidateAsync(
                    _packageMock.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(ValidationStatus.Failed, PackageSigningStatus.Invalid);
            }

            [Fact]
            public async Task RejectsSignedPackagesWithMultipleSignatures()
            {
                // Arrange
                var signatures = (await TestResources.SignedPackageLeaf1Reader.GetSignaturesAsync(CancellationToken.None))
                    .Concat(await TestResources.SignedPackageLeaf2Reader.GetSignaturesAsync(CancellationToken.None))
                    .ToList();

                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _packageMock
                    .Setup(x => x.GetSignaturesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(signatures);
                _certificates
                    .Setup(x => x.GetAll())
                    .Returns(new[] { TestResources.Leaf1Thumbprint, TestResources.Leaf2Thumbprint }
                        .Select(x => new Certificate { Thumbprint = x })
                        .AsQueryable());

                // Act
                await _target.ValidateAsync(
                    _packageMock.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(ValidationStatus.Failed, PackageSigningStatus.Invalid);
            }

            [Fact]
            public async Task AcceptsUnsignedPackages()
            {
                // Arrange
                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                // Act
                await _target.ValidateAsync(
                    _packageMock.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Validate(ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            }
        }
    }
}
