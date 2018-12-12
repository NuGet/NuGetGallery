// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class PackageSigningStateServiceFacts
    {
        public class TheSetPackageSigningStateMethod
        {
            private readonly ILoggerFactory _loggerFactory;

            public TheSetPackageSigningStateMethod(ITestOutputHelper testOutput)
            {
                _loggerFactory = new LoggerFactory();
                _loggerFactory.AddXunit(testOutput);
            }

            [Fact]
            public async Task UpdatesExistingStateWhenSignatureStateNotNullAndRevalidating()
            {
                // Arrange
                const int packageKey = 1;
                const string packageId = "packageId";
                const string packageVersion = "1.0.0";
                const PackageSigningStatus newStatus = PackageSigningStatus.Invalid;
                var packageSigningState = new PackageSigningState
                {
                    PackageId = packageId,
                    PackageKey = packageKey,
                    SigningStatus = PackageSigningStatus.Unsigned,
                    PackageNormalizedVersion = packageVersion
                };

                var logger = _loggerFactory.CreateLogger<PackageSigningStateService>();
                var packageSigningStatesDbSetMock = DbSetMockFactory.Create(packageSigningState);
                var validationContextMock = new Mock<IValidationEntitiesContext>(MockBehavior.Strict);
                validationContextMock.Setup(m => m.PackageSigningStates).Returns(packageSigningStatesDbSetMock);

                // Act
                var packageSigningStateService = new PackageSigningStateService(validationContextMock.Object, logger);

                // Assert
                await packageSigningStateService.SetPackageSigningState(
                    packageKey,
                    packageId,
                    packageVersion,
                    status: newStatus);

                // Assert
                Assert.Equal(newStatus, packageSigningState.SigningStatus);
                validationContextMock.Verify(
                    m => m.SaveChangesAsync(),
                    Times.Never,
                    "Saving the context here is incorrect as updating the validator's status also saves the context. Doing so would cause both queries not to be executed in the same transaction.");
            }

            [Fact]
            public async Task DropsAllPackageSignaturesWhenPackageStateTransitionsToUnsigned()
            {
                // Arrange
                const int packageKey = 1;
                const string packageId = "packageId";
                const string packageVersion = "1.0.0";
                const PackageSigningStatus newStatus = PackageSigningStatus.Unsigned;

                var signature1 = new PackageSignature();
                var signature2 = new PackageSignature();

                var packageSigningState = new PackageSigningState
                {
                    PackageId = packageId,
                    PackageKey = packageKey,
                    SigningStatus = PackageSigningStatus.Valid,
                    PackageNormalizedVersion = packageVersion,

                    PackageSignatures = new List<PackageSignature> { signature1, signature2 }
                };

                var logger = _loggerFactory.CreateLogger<PackageSigningStateService>();
                var packageSigningStatesDbSetMock = DbSetMockFactory.CreateMock(packageSigningState);
                var packageSignaturesDbSetMock = DbSetMockFactory.CreateMock(signature1, signature2);
                var validationContextMock = new Mock<IValidationEntitiesContext>(MockBehavior.Strict);
                validationContextMock.Setup(m => m.PackageSigningStates).Returns(packageSigningStatesDbSetMock.Object);
                validationContextMock.Setup(m => m.PackageSignatures).Returns(packageSignaturesDbSetMock.Object);

                // Act
                var packageSigningStateService = new PackageSigningStateService(validationContextMock.Object, logger);

                // Assert
                await packageSigningStateService.SetPackageSigningState(
                    packageKey,
                    packageId,
                    packageVersion,
                    status: newStatus);

                // Assert
                Assert.Equal(newStatus, packageSigningState.SigningStatus);

                packageSignaturesDbSetMock.Verify(m => m.Remove(signature1), Times.Once);
                packageSignaturesDbSetMock.Verify(m => m.Remove(signature2), Times.Once);

                validationContextMock.Verify(
                    m => m.SaveChangesAsync(),
                    Times.Never,
                    "Saving the context here is incorrect as updating the validator's status also saves the context. Doing so would cause both queries not to be executed in the same transaction.");
            }

            [Fact]
            public async Task AddsNewStateWhenSignatureStateIsNull()
            {
                // Arrange
                const int packageKey = 1;
                const string packageId = "packageId";
                const string packageVersion = "1.0.0";
                const PackageSigningStatus newStatus = PackageSigningStatus.Invalid;

                var logger = _loggerFactory.CreateLogger<PackageSigningStateService>();
                var packageSigningStatesDbSetMock = DbSetMockFactory.Create<PackageSigningState>();
                var validationContextMock = new Mock<IValidationEntitiesContext>(MockBehavior.Strict);
                validationContextMock.Setup(m => m.PackageSigningStates).Returns(packageSigningStatesDbSetMock);

                // Act
                var packageSigningStateService = new PackageSigningStateService(validationContextMock.Object, logger);

                // Assert
                await packageSigningStateService.SetPackageSigningState(
                    packageKey,
                    packageId,
                    packageVersion,
                    status: newStatus);

                // Assert
                var newState = validationContextMock.Object.PackageSigningStates.FirstOrDefault();
                Assert.NotNull(newState);
                Assert.Equal(packageKey, newState.PackageKey);
                Assert.Equal(packageId, newState.PackageId);
                Assert.Equal(packageVersion, newState.PackageNormalizedVersion);
                Assert.Equal(newStatus, newState.SigningStatus);
                validationContextMock.Verify(
                    m => m.SaveChangesAsync(),
                    Times.Never,
                    "Saving the context here is incorrect as updating the validator's status also saves the context. Doing so would cause both queries not to be executed in the same transaction.");
            }
        }
    }
}