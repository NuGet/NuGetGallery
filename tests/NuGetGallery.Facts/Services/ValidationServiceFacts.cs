// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Moq;
using NuGet.Services.Validation;
using Xunit;

namespace NuGetGallery
{
    public class ValidationServiceFacts
    {
        public class TheStartValidationMethod : FactsBase
        {
            [Fact]
            public async Task InitiatesTheValidation()
            {
                // Arrange & Act
                await _target.StartValidationAsync(_package);

                // Assert
                _initiator.Verify(x => x.StartValidationAsync(_package), Times.Once);
            }

            [Fact]
            public async Task UpdatesThePackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _package.PackageStatusKey = PackageStatus.Available;
                _initiator
                    .Setup(x => x.StartValidationAsync(It.IsAny<Package>()))
                    .ReturnsAsync(packageStatus);

                // Act
                await _target.StartValidationAsync(_package);

                // Assert
                _packageService.Verify(
                    x => x.UpdatePackageStatusAsync(_package, packageStatus, false),
                    Times.Once);

                /// The implementation should not change the package status on its own. It should depend on 
                /// <see cref="IPackageService"/> to do this.
                Assert.Equal(PackageStatus.Available, _package.PackageStatusKey);
            }
        }

        public class TheRevalidateMethod : FactsBase
        {
            [Fact]
            public async Task InitiatesTheValidation()
            {
                // Arrange & Act
                await _target.RevalidateAsync(_package);

                // Assert
                _initiator.Verify(x => x.StartValidationAsync(_package), Times.Once);
            }

            [Fact]
            public async Task DoesNotChangeThePackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _package.PackageStatusKey = PackageStatus.Available;
                _initiator
                    .Setup(x => x.StartValidationAsync(It.IsAny<Package>()))
                    .ReturnsAsync(packageStatus);

                // Act
                await _target.RevalidateAsync(_package);

                // Assert
                _packageService.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);

                /// The implementation should not change the package status on its own. It should depend on 
                /// <see cref="IPackageService"/> to do this.
                Assert.Equal(PackageStatus.Available, _package.PackageStatusKey);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageValidationInitiator> _initiator;
            protected readonly Package _package;
            protected readonly ValidationService _target;

            public FactsBase()
            {
                _packageService = new Mock<IPackageService>();
                _initiator = new Mock<IPackageValidationInitiator>();

                _package = new Package();

                var validationContext = new Mock<IValidationEntitiesContext>();

                _target = new ValidationService(
                    _packageService.Object,
                    _initiator.Object,
                    validationContext.Object);
            }
        }
    }
}
