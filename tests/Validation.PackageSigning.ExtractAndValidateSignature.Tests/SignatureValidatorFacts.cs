// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignatureValidatorFacts
    {
        public class ValidateAsync
        {
            private readonly Mock<ISignedPackageReader> _package;
            private readonly ValidatorStatus _validation;
            private readonly SignatureValidationMessage _message;
            private readonly CancellationToken _cancellationToken;
            private readonly Mock<IPackageSigningStateService> _packageSigningStateService;
            private readonly Mock<ILogger<SignatureValidator>> _logger;
            private readonly SignatureValidator _target;

            public ValidateAsync()
            {
                _package = new Mock<ISignedPackageReader>();
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
                _logger = new Mock<ILogger<SignatureValidator>>();

                _target = new SignatureValidator(
                    _packageSigningStateService.Object,
                    _logger.Object);
            }

            [Fact]
            public async Task RejectsSsignedPackages()
            {
                // Arrange
                _package
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                // Act
                await _target.ValidateAsync(
                    _package.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Assert.Equal(ValidationStatus.Failed, _validation.State);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageSigningStatus>()),
                    Times.Never);
            }

            [Fact]
            public async Task AcceptsUnsignedPackages()
            {
                // Arrange
                _package
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                // Act
                await _target.ValidateAsync(
                    _package.Object,
                    _validation,
                    _message,
                    _cancellationToken);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, _validation.State);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        _validation.PackageKey,
                        _message.PackageId,
                        _message.PackageVersion,
                        PackageSigningStatus.Unsigned),
                    Times.Once);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageSigningStatus>()),
                    Times.Once);
            }
        }
    }
}
