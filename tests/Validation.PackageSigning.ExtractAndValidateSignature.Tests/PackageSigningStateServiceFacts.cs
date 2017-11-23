// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class PackageSigningStateServiceFacts
    {
        public class TheTrySetPackageSigningStateMethod
        {
            private readonly ILoggerFactory _loggerFactory;

            public TheTrySetPackageSigningStateMethod(ITestOutputHelper testOutput)
            {
                _loggerFactory = new LoggerFactory();
                _loggerFactory.AddXunit(testOutput);
            }

            [Fact]
            public async Task ReturnsStatusAlreadyExistsWhenSignatureStateNotNullAndNotRevalidating()
            {
                // Arrange
                const int packageKey = 1;
                const string packageId = "packageId";
                const string packageVersion = "1.0.0";
                var packageSigningState = new PackageSigningState
                {
                    PackageId = packageId,
                    PackageKey = packageKey,
                    SigningStatus = PackageSigningStatus.Valid,
                    PackageNormalizedVersion = packageVersion
                };

                var logger = _loggerFactory.CreateLogger<PackageSigningStateService>();
                var packageSigningStatesDbSetMock = DbSetMockFactory.Create(packageSigningState);
                var validationContextMock = new Mock<IValidationEntitiesContext>(MockBehavior.Strict);
                validationContextMock.Setup(m => m.PackageSigningStates).Returns(packageSigningStatesDbSetMock);

                // Act
                var packageSigningStateService = new PackageSigningStateService(validationContextMock.Object, logger);

                // Assert
                var result = await packageSigningStateService.TrySetPackageSigningState(
                    packageKey,
                    packageId,
                    packageVersion,
                    isRevalidationRequest: false,
                    status: PackageSigningStatus.Valid);

                // Assert
                Assert.Equal(SavePackageSigningStateResult.StatusAlreadyExists, result);
                validationContextMock.Verify(
                    m => m.SaveChangesAsync(),
                    Times.Never,
                    "Saving the context here is unnecessary.");
            }

            [Fact]
            public async Task ReturnsStatusSuccessAndUpdatedExistingStateWhenSignatureStateNotNullAndRevalidating()
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
                var result = await packageSigningStateService.TrySetPackageSigningState(
                    packageKey,
                    packageId,
                    packageVersion,
                    isRevalidationRequest: true,
                    status: newStatus);

                // Assert
                Assert.Equal(SavePackageSigningStateResult.Success, result);
                Assert.Equal(newStatus, packageSigningState.SigningStatus);
                validationContextMock.Verify(
                    m => m.SaveChangesAsync(),
                    Times.Never,
                    "Saving the context here is incorrect as updating the validator's status also saves the context. Doing so would cause both queries not to be executed in the same transaction.");
            }

            [Fact]
            public async Task ReturnsStatusSuccessAndAddedNewStateWhenSignatureStateIsNull()
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
                var result = await packageSigningStateService.TrySetPackageSigningState(
                    packageKey,
                    packageId,
                    packageVersion,
                    isRevalidationRequest: true,
                    status: newStatus);

                // Assert
                Assert.Equal(SavePackageSigningStateResult.Success, result);

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