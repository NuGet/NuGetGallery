// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery.Configuration;
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
                _packageInitiator.Verify(x => x.StartValidationAsync(_package), Times.Once);
            }

            [Fact]
            public async Task UpdatesThePackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _package.PackageStatusKey = PackageStatus.Available;
                _packageInitiator
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

        public class TheUpdatePackageAsyncMethod : FactsBase
        {
            public async Task UpdatesPackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _package.PackageStatusKey = PackageStatus.Available;
                _packageInitiator
                    .Setup(x => x.StartValidationAsync(It.IsAny<Package>()))
                    .ReturnsAsync(packageStatus);

                // Act
                await _target.UpdatePackageAsync(_package);

                // Assert
                _packageService.Verify(
                    x => x.UpdatePackageStatusAsync(_package, packageStatus, false),
                    Times.Once);

                /// The implementation should not change the package status on its own. It should depend on 
                /// <see cref="IPackageService"/> to do this.
                Assert.Equal(PackageStatus.Available, _package.PackageStatusKey);
            }
        }

        public class TheStartSymbolsPackageValidationAsyncMethod : FactsBase
        {
            [Fact]
            public async Task InitiatesTheValidation()
            {
                // Arrange & Act
                await _target.StartValidationAsync(_symbolPackage);

                // Assert
                _symbolInitiator.Verify(x => x.StartValidationAsync(_symbolPackage), Times.Once);
            }

            [Fact]
            public async Task UpdatesThePackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _symbolPackage.StatusKey = PackageStatus.Available;
                _symbolInitiator
                    .Setup(x => x.StartValidationAsync(It.IsAny<SymbolPackage>()))
                    .ReturnsAsync(packageStatus);

                // Act
                await _target.StartValidationAsync(_symbolPackage);

                // Assert
                _symbolPackageService.Verify(
                    x => x.UpdateStatusAsync(_symbolPackage, packageStatus, false),
                    Times.Once);

                /// The implementation should not change the package status on its own. It should depend on 
                /// <see cref="ISymbolPackageService"/> to do this.
                Assert.Equal(PackageStatus.Available, _symbolPackage.StatusKey);
            }
        }

        public class TheUpdateSymbolPackageAsyncMethod : FactsBase
        {
            public async Task UpdatesPackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _symbolPackage.StatusKey = PackageStatus.Available;
                _symbolInitiator
                    .Setup(x => x.StartValidationAsync(It.IsAny<SymbolPackage>()))
                    .ReturnsAsync(packageStatus);

                // Act
                await _target.UpdatePackageAsync(_symbolPackage);

                // Assert
                _symbolPackageService.Verify(
                    x => x.UpdateStatusAsync(_symbolPackage, packageStatus, false),
                    Times.Once);

                /// The implementation should not change the package status on its own. It should depend on 
                /// <see cref="IPackageService"/> to do this.
                Assert.Equal(PackageStatus.Available, _symbolPackage.StatusKey);
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
                _packageInitiator.Verify(x => x.StartValidationAsync(_package), Times.Once);
            }

            [Fact]
            public async Task DoesNotChangeThePackageStatus()
            {
                // Arrange
                var packageStatus = PackageStatus.Validating;
                _package.PackageStatusKey = PackageStatus.Available;
                _packageInitiator
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

            [Fact]
            public async Task EmitsTelemetry()
            {
                // Arrange & Act
                await _target.RevalidateAsync(_package);

                // Assert
                _telemetryService.Verify(x => x.TrackPackageRevalidate(_package), Times.Once);
            }
        }

        public class TheIsValidatingTooLongMethod : FactsBase
        {
            public static IEnumerable<object[]> ReturnsTrueIfPackageIsValidatingTooLongData()
            {
                yield return new object[]
                {
                    new Package
                    {
                        PackageStatusKey = PackageStatus.Validating,
                        Created = DateTime.UtcNow - TimeSpan.FromHours(1),
                    },

                    true,
                };

                yield return new object[]
                {
                    new Package
                    {
                        PackageStatusKey = PackageStatus.Validating,
                        Created = DateTime.UtcNow - TimeSpan.FromMinutes(29),
                    },

                    false,
                };

                // Packages whose status is not "Validating" should NEVER return true
                foreach (var status in new[] { PackageStatus.Available, PackageStatus.Deleted, PackageStatus.FailedValidation })
                {
                    yield return new object[]
                    {
                        new Package
                        {
                            PackageStatusKey = status,
                            Created = DateTime.UtcNow - TimeSpan.FromHours(1),
                        },

                        false,
                    };
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsTrueIfPackageIsValidatingTooLongData))]
            public void ReturnsTrueIfPackageIsValidatingTooLong(Package package, bool expectedResult)
            {
                _appConfiguration
                    .Setup(c => c.ValidationExpectedTime)
                    .Returns(TimeSpan.FromMinutes(30));

                if (expectedResult)
                {
                    Assert.True(_target.IsValidatingTooLong(package));
                }
                else
                {
                    Assert.False(_target.IsValidatingTooLong(package));
                }
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
                var issues = _target.GetLatestPackageValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Never());
            }

            [Fact]
            public void ReturnsEmptyListForNullSymbolsPackage()
            {
                // Arrange and act
                var issues = _target.GetLatestPackageValidationIssues(symbolPackage: null);

                Assert.NotNull(issues);
                Assert.Empty(issues);
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
                                    Data = ValidationIssue.PackageIsSigned.Serialize(),
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
                var issues = _target.GetLatestPackageValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Once);

                Assert.Equal(4, issues.Count);

                Assert.Equal(ValidationIssueCode.PackageIsSigned, issues[0].IssueCode);

                var issue1 = Assert.IsType<ClientSigningVerificationFailure>(issues[1]);
                Assert.Equal("NU3001", issue1.ClientCode);
                Assert.Equal("Different.", issue1.ClientMessage);

                var issue2 = Assert.IsType<ClientSigningVerificationFailure>(issues[2]);
                Assert.Equal("NU3000", issue2.ClientCode);
                Assert.Equal("Please endorse.", issue2.ClientMessage);

                Assert.Equal(ValidationIssueCode.Unknown, issues[3].IssueCode);
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
                            PackageValidationIssues = Array.Empty<PackageValidationIssue>(),
                        }
                    }
                };

                _validationSets
                    .Setup(x => x.GetAll())
                    .Returns(new[] { packageValidationSet }.AsQueryable());

                // Act
                var issues = _target.GetLatestPackageValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Once);

                var issue = Assert.Single(issues);
                Assert.Equal(ValidationIssueCode.Unknown, issue.IssueCode);
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
                var issues = _target.GetLatestPackageValidationIssues(_package);

                // Assert
                _validationSets.Verify(x => x.GetAll(), Times.Once());

                Assert.Single(issues);
                Assert.Equal(ValidationIssueCode.PackageIsSigned, issues.First().IssueCode);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IAppConfiguration> _appConfiguration;
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageValidationInitiator<Package>> _packageInitiator;
            protected readonly Mock<IPackageValidationInitiator<SymbolPackage>> _symbolInitiator;
            protected readonly Mock<IEntityRepository<PackageValidationSet>> _validationSets;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<ISymbolPackageService> _symbolPackageService;
            protected readonly Package _package;
            protected readonly ValidationService _target;
            protected readonly SymbolPackage _symbolPackage;

            public FactsBase()
            {
                _appConfiguration = new Mock<IAppConfiguration>();
                _packageService = new Mock<IPackageService>();
                _packageInitiator = new Mock<IPackageValidationInitiator<Package>>();
                _symbolInitiator = new Mock<IPackageValidationInitiator<SymbolPackage>>();
                _validationSets = new Mock<IEntityRepository<PackageValidationSet>>();
                _telemetryService = new Mock<ITelemetryService>();
                _symbolPackageService = new Mock<ISymbolPackageService>();
                _package = new Package();
                _symbolPackage = new SymbolPackage()
                    {
                        Package = _package
                    };

                _target = new ValidationService(
                    _appConfiguration.Object,
                    _packageService.Object,
                    _packageInitiator.Object,
                    _symbolInitiator.Object,
                    _telemetryService.Object,
                    _symbolPackageService.Object,
                    _validationSets.Object);
            }
        }
    }
}
