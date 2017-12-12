// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
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

        public class TheGetLatestValidationIssuesMethod : FactsBase
        {
            [Theory]
            [InlineData(PackageStatus.Available)]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.Validating)]
            public void IgnoresPackagesThatHaventFailedValidations(PackageStatus status)
            {
                // Arrange
                _package.PackageStatusKey = status;

                // Act
                var issues = _target.GetLatestValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Never());
            }

            [Fact]
            public void FetchesValidationIssues()
            {
                // Arrange
                _package.Key = 123;
                _package.PackageStatusKey = PackageStatus.FailedValidation;

                // This is the latest failed validation set. Its issues should be fetched.
                var packageValidationSet1 = new PackageValidationSet
                {
                    PackageKey = 123,
                    Updated = DateTime.UtcNow.AddDays(-5),
                };

                // This is an old validation set. Its issues should be ignored.
                var packageValidationSet2 = new PackageValidationSet
                {
                    PackageKey = 123,
                    Updated = DateTime.UtcNow.AddDays(-10),
                };

                // This is a validation set for anotehr package. Its issues should be ignored.
                var packageValidationSet3 = new PackageValidationSet
                {
                    PackageKey = 456,
                    Updated = DateTime.UtcNow,
                };

                var packageValidation1 = new PackageValidation { ValidationStatus = ValidationStatus.Failed };
                var packageValidation2 = new PackageValidation { ValidationStatus = ValidationStatus.Failed };
                var packageValidation3 = new PackageValidation { ValidationStatus = ValidationStatus.Failed };

                var validationIssue1 = new PackageValidationIssue
                {
                    IssueCode = ValidationIssueCode.PackageIsSigned,
                    Data = "{'packageId':'Hello','packageVersion':'World'}",
                };

                var validationIssue2 = new PackageValidationIssue
                {
                    IssueCode = ValidationIssueCode.PackageIsSigned,
                    Data = "{'packageId':'A moose once bit my sister','packageVersion':'1.2.3'}",
                };

                var validationIssue3 = new PackageValidationIssue
                {
                    IssueCode = ValidationIssueCode.PackageIsSigned,
                    Data = "{'packageId':'Moose bites can be pretty nasty','packageVersion':'4.5.6'}",
                };

                packageValidationSet1.PackageValidations = new[] { packageValidation1 };
                packageValidationSet2.PackageValidations = new[] { packageValidation2 };
                packageValidationSet3.PackageValidations = new[] { packageValidation3 };

                packageValidation1.PackageValidationIssues = new[] { validationIssue1 };
                packageValidation2.PackageValidationIssues = new[] { validationIssue2 };
                packageValidation3.PackageValidationIssues = new[] { validationIssue3 };

                _validationSets.Setup(x => x.GetAll())
                               .Returns(new[] { packageValidationSet1, packageValidationSet2, packageValidationSet3 }.AsQueryable());

                // Act
                var issues = _target.GetLatestValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Once());

                Assert.Equal(1, issues.Count());
                Assert.Equal("Package Hello World is signed.", issues.First().GetMessage());
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageValidationInitiator> _initiator;
            protected readonly Mock<IEntityRepository<PackageValidationSet>> _validationSets;
            protected readonly Package _package;
            protected readonly ValidationService _target;

            public FactsBase()
            {
                _packageService = new Mock<IPackageService>();
                _initiator = new Mock<IPackageValidationInitiator>();
                _validationSets = new Mock<IEntityRepository<PackageValidationSet>>();

                _package = new Package();

                _target = new ValidationService(
                    _packageService.Object,
                    _initiator.Object,
                    _validationSets.Object);
            }
        }
    }
}
