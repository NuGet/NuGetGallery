// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin.Models;
using Xunit;

namespace NuGetGallery.Areas.Admin.Services
{
    public class ValidationAdminServiceFacts
    {
        public class TheSearchMethod : FactsBase
        {
            [Fact]
            public void DoesNotReturnDuplicatePackageValidationSets()
            {
                // Arrange
                var query = $"{_validationKey}\n{_validationSetTrackingId}";

                // Act
                var actual = _target.Search(query);

                // Assert
                Assert.Equal(1, actual.Count);
                Assert.Same(_validationSet, actual[0]);
            }
        }

        public class TheRevalidatePendingMethod : FactsBase
        {
            [Fact]
            public async Task RevalidatesValidatingPackagesAsync()
            {
                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[]
                    {
                        new Package { Key = 1, PackageStatusKey = PackageStatus.Available },
                        new Package { Key = 2, PackageStatusKey = PackageStatus.Validating },
                        new Package { Key = 3, PackageStatusKey = PackageStatus.Validating },
                        new Package { Key = 4, PackageStatusKey = PackageStatus.Deleted },
                        new Package { Key = 5, PackageStatusKey = PackageStatus.FailedValidation },
                    }.AsQueryable());

                var revalidatedCount = await _target.RevalidatePendingAsync(ValidatingType.Package);

                Assert.Equal(2, revalidatedCount);
                _validationService.Verify(x => x.RevalidateAsync(It.IsAny<Package>()), Times.Exactly(2));
                _validationService.Verify(x => x.RevalidateAsync(It.Is<Package>(p => p.Key == 2)), Times.Once);
                _validationService.Verify(x => x.RevalidateAsync(It.Is<Package>(p => p.Key == 3)), Times.Once);
            }

            [Fact]
            public async Task RevalidatesValidatingSymbolsPackagesAsync()
            {
                _symbolPackages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[]
                    {
                        new SymbolPackage { Key = 1, StatusKey = PackageStatus.Available },
                        new SymbolPackage { Key = 2, StatusKey = PackageStatus.Validating },
                        new SymbolPackage { Key = 3, StatusKey = PackageStatus.Validating },
                        new SymbolPackage { Key = 4, StatusKey = PackageStatus.Deleted },
                        new SymbolPackage { Key = 5, StatusKey = PackageStatus.FailedValidation },
                    }.AsQueryable());

                var revalidatedCount = await _target.RevalidatePendingAsync(ValidatingType.SymbolPackage);

                Assert.Equal(2, revalidatedCount);
                _validationService.Verify(x => x.RevalidateAsync(It.IsAny<SymbolPackage>()), Times.Exactly(2));
                _validationService.Verify(x => x.RevalidateAsync(It.Is<SymbolPackage>(p => p.Key == 2)), Times.Once);
                _validationService.Verify(x => x.RevalidateAsync(It.Is<SymbolPackage>(p => p.Key == 3)), Times.Once);
            }

            [Fact]
            public async Task RejectsUnknownPackageStatusKey()
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => _target.RevalidatePendingAsync(ValidatingType.Generic));
            }
        }

        public class TheGetPackageDeletedStatusMethod : FactsBase
        {
            [Fact]
            public void ReturnsUnknownForMissingPackage()
            {
                // Arrange
                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => Enumerable.Empty<Package>().AsQueryable());

                // Act
                var status = _target.GetPackageDeletedStatus(_packageKey);

                // Assert
                Assert.Equal(PackageDeletedStatus.Unknown, status);
            }

            [Fact]
            public void ReturnsNotDeletedForNonDeletedPackage()
            {
                // Arrange
                _package.PackageStatusKey = PackageStatus.Available;

                // Act
                var status = _target.GetPackageDeletedStatus(_packageKey);

                // Assert
                Assert.Equal(PackageDeletedStatus.NotDeleted, status);
            }

            [Fact]
            public void ReturnsSoftDeletedForDeletedPackage()
            {
                // Arrange
                _package.PackageStatusKey = PackageStatus.Deleted;

                // Act
                var status = _target.GetPackageDeletedStatus(_packageKey);

                // Assert
                Assert.Equal(PackageDeletedStatus.SoftDeleted, status);
            }
        }

        public class TheGetSymbolsPackageDeletedStatusMethod : FactsBase
        {
            [Fact]
            public void ReturnsUnknownForMissingPackage()
            {
                // Arrange
                _symbolPackages
                    .Setup(x => x.GetAll())
                    .Returns(() => Enumerable.Empty < SymbolPackage>().AsQueryable());

                // Act
                var status = _target.GetSymbolPackageDeletedStatus(_symbolPackageKey);

                // Assert
                Assert.Equal(PackageDeletedStatus.Unknown, status);
            }

            [Fact]
            public void ReturnsNotDeletedForNonDeletedPackage()
            {
                // Arrange
                _symbolPackage.StatusKey = PackageStatus.Available;

                // Act
                var status = _target.GetSymbolPackageDeletedStatus(_symbolPackageKey);

                // Assert
                Assert.Equal(PackageDeletedStatus.NotDeleted, status);
            }

            [Fact]
            public void ReturnsSoftDeletedForDeletedPackage()
            {
                // Arrange
                _symbolPackage.StatusKey = PackageStatus.Deleted;

                // Act
                var status = _target.GetSymbolPackageDeletedStatus(_symbolPackageKey);

                // Assert
                Assert.Equal(PackageDeletedStatus.SoftDeleted, status);
            }
        }

        public abstract class FactsBase
        {
            protected readonly int _packageKey;
            protected readonly Package _package;
            protected readonly int _symbolPackageKey;
            protected readonly SymbolPackage _symbolPackage;
            protected readonly Guid _validationKey;
            protected readonly PackageValidation _validation;
            protected readonly int _validationSetKey;
            protected readonly Guid _validationSetTrackingId;
            protected readonly PackageValidationSet _validationSet;
            protected readonly Mock<IEntityRepository<PackageValidationSet>> _validationSets;
            protected readonly Mock<IEntityRepository<PackageValidation>> _validations;
            protected readonly Mock<IEntityRepository<Package>> _packages;
            protected readonly Mock<IEntityRepository<SymbolPackage>> _symbolPackages;
            protected readonly Mock<IValidationService> _validationService;
            protected readonly ValidationAdminService _target;

            public FactsBase()
            {
                _packageKey = 42;
                _symbolPackageKey = 420;
                _package = new Package { Key = _packageKey };
                _symbolPackage = new SymbolPackage() { Key = _symbolPackageKey };
                _validationKey = new Guid("ae05c5f9-eb2a-415b-ae42-92829bf201a7");
                _validation = new PackageValidation
                {
                    Key = _validationKey
                };
                _validationSetKey = 1001;
                _validationSetTrackingId = new Guid("490e8d72-967a-485f-a035-67d5bba0af9f");
                _validationSet = new PackageValidationSet
                {
                    Key = _validationSetKey,
                    ValidationTrackingId = _validationSetTrackingId,
                    PackageValidations = new[] { _validation },
                };
                _validation.PackageValidationSet = _validationSet;

                _validationSets = new Mock<IEntityRepository<PackageValidationSet>>();
                _validations = new Mock<IEntityRepository<PackageValidation>>();
                _packages = new Mock<IEntityRepository<Package>>();
                _symbolPackages = new Mock<IEntityRepository<SymbolPackage>>();
                _validationService = new Mock<IValidationService>();

                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _package }.AsQueryable());
                _symbolPackages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _symbolPackage }.AsQueryable());
                _validations
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _validation }.AsQueryable());
                _validationSets
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _validationSet }.AsQueryable());

                _target = new ValidationAdminService(
                    _validationSets.Object,
                    _validations.Object,
                    _packages.Object,
                    _symbolPackages.Object,
                    _validationService.Object);
            }
        }
    }
}
