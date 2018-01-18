// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
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
            public void DeduplicatesValidationIssuesByCodeAndData()
            {
                // Arrange
                _package.Key = 123;
                _package.PackageStatusKey = PackageStatus.FailedValidation;

                var packageValidationSet = new PackageValidationSet
                {
                    PackageKey = 123,
                    PackageValidations = new[]
                    {
                        new PackageValidation
                        {
                            ValidationStatus = ValidationStatus.Failed,
                            PackageValidationIssues = new[]
                            {
                                new PackageValidationIssue
                                {
                                    Key = 5,
                                    IssueCode = ValidationIssueCode.Unknown,
                                    Data = "{}",
                                },
                                new PackageValidationIssue // Acceptable PackageIsSigned since unknown properties are ignored.
                                {
                                    Key = 1,
                                    IssueCode = ValidationIssueCode.PackageIsSigned,
                                    Data = "{\"foo\":\"bar\"}",
                                },
                                new PackageValidationIssue
                                {
                                    Key = 4,
                                    IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                    Data = new ClientSigningVerificationFailure("NU3000", "Please endorse.").Serialize(),
                                },
                                new PackageValidationIssue // Duplicate issue since there is another PackageIsSigned.
                                {
                                    Key = 2,
                                    IssueCode = ValidationIssueCode.PackageIsSigned,
                                    Data = new PackageIsSigned().Serialize(),
                                },
                                new PackageValidationIssue
                                {
                                    Key = 3,
                                    IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                    Data = new ClientSigningVerificationFailure("NU3001", "Different.").Serialize(),
                                },
                                new PackageValidationIssue // Duplicate of UnknownIssue since the data is invalid.
                                {
                                    Key = 6,
                                    IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                    Data = "[]",
                                },
                                new PackageValidationIssue // Duplicate of UnknownIssue since the data is invalid.
                                {
                                    Key = 7,
                                    IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                    Data = "{\"bad\":23}",
                                },
                            }
                        }
                    }
                };

                _validationSets
                    .Setup(x => x.GetAll())
                    .Returns(new[] { packageValidationSet }.AsQueryable());

                // Act
                var issues = _target.GetLatestValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Once);

                Assert.Equal(4, issues.Count);

                Assert.IsType<PackageIsSigned>(issues[0]);

                var issue1 = Assert.IsType<ClientSigningVerificationFailure>(issues[1]);
                Assert.Equal("NU3001", issue1.ClientCode);
                Assert.Equal("Different.", issue1.ClientMessage);

                var issue2 = Assert.IsType<ClientSigningVerificationFailure>(issues[2]);
                Assert.Equal("NU3000", issue2.ClientCode);
                Assert.Equal("Please endorse.", issue2.ClientMessage);

                var issue3 = Assert.IsType<UnknownIssue>(issues[3]);
            }

            [Fact]
            public void ReturnsSingleUnknownIssueIfNoneArePersisted()
            {
                // Arrange
                _package.Key = 123;
                _package.PackageStatusKey = PackageStatus.FailedValidation;

                var packageValidationSet = new PackageValidationSet
                {
                    PackageKey = 123,
                    PackageValidations = new[]
                    {
                        new PackageValidation
                        {
                            ValidationStatus = ValidationStatus.Failed,
                            PackageValidationIssues = new PackageValidationIssue[0],
                        }
                    }
                };

                _validationSets
                    .Setup(x => x.GetAll())
                    .Returns(new[] { packageValidationSet }.AsQueryable());

                // Act
                var issues = _target.GetLatestValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Once);

                var issue = Assert.Single(issues);
                Assert.IsType<UnknownIssue>(issue);
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
                    Data = "{}",
                };

                var validationIssue2 = new PackageValidationIssue
                {
                    IssueCode = ValidationIssueCode.PackageIsSigned,
                    Data = "{}",
                };

                var validationIssue3 = new PackageValidationIssue
                {
                    IssueCode = ValidationIssueCode.PackageIsSigned,
                    Data = "{}",
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
                Assert.Equal(ValidationIssueCode.PackageIsSigned, issues.First().IssueCode);
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
