﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Moq;
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

        public abstract class FactsBase
        {
            protected readonly int _packageKey;
            protected readonly Package _package;
            protected readonly Guid _validationKey;
            protected readonly PackageValidation _validation;
            protected readonly int _validationSetKey;
            protected readonly Guid _validationSetTrackingId;
            protected readonly PackageValidationSet _validationSet;
            protected readonly Mock<IEntityRepository<PackageValidationSet>> _validationSets;
            protected readonly Mock<IEntityRepository<PackageValidation>> _validations;
            protected readonly Mock<IEntityRepository<Package>> _packages;
            protected readonly ValidationAdminService _target;

            public FactsBase()
            {
                _packageKey = 42;
                _package = new Package { Key = _packageKey };
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

                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _package }.AsQueryable());
                _validations
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _validation }.AsQueryable());
                _validationSets
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _validationSet }.AsQueryable());

                _target = new ValidationAdminService(
                    _validationSets.Object,
                    _validations.Object,
                    _packages.Object);
            }
        }
    }
}
