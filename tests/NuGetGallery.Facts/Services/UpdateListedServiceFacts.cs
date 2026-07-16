// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Services
{
    public class UpdateListedServiceFacts
    {
        public class TheUpdateListedAsyncMethod
        {
            private readonly Mock<IPackageService> _packageService;
            private readonly Mock<IPackageUpdateService> _packageUpdateService;
            private readonly UpdateListedService _target;

            private readonly PackageRegistration _registrationA;
            private readonly PackageRegistration _registrationB;

            public TheUpdateListedAsyncMethod()
            {
                _packageService = new Mock<IPackageService>();
                _packageUpdateService = new Mock<IPackageUpdateService>();
                _target = new UpdateListedService(_packageService.Object, _packageUpdateService.Object);

                _registrationA = new PackageRegistration
                {
                    Key = 1,
                    Id = "NuGet.Versioning",
                };
                _registrationA.Packages = new List<Package>
                {
                    new Package
                    {
                        Key = 1,
                        NormalizedVersion = "4.3.0",
                        PackageRegistration = _registrationA,
                        PackageRegistrationKey = _registrationA.Key,
                    },
                    new Package
                    {
                        Key = 2,
                        NormalizedVersion = "4.4.0",
                        PackageRegistration = _registrationA,
                        PackageRegistrationKey = _registrationA.Key,
                    },
                    new Package
                    {
                        Key = 3,
                        NormalizedVersion = "4.5.0",
                        PackageRegistration = _registrationA,
                        PackageRegistrationKey = _registrationA.Key,
                    },
                };

                _registrationB = new PackageRegistration
                {
                    Key = 2,
                    Id = "NuGet.Frameworks",
                };
                _registrationB.Packages = new List<Package>
                {
                    new Package
                    {
                        Key = 4,
                        NormalizedVersion = "5.3.0",
                        PackageRegistration = _registrationB,
                        PackageRegistrationKey = _registrationB.Key,
                    },
                    new Package
                    {
                        Key = 5,
                        NormalizedVersion = "5.4.0",
                        PackageRegistration = _registrationB,
                        PackageRegistrationKey = _registrationB.Key,
                    },
                };

                _packageService
                    .Setup(x => x.FindPackagesById("NuGet.Versioning", It.IsAny<PackageDeprecationFieldsToInclude>()))
                    .Returns(() => (IReadOnlyCollection<Package>)_registrationA.Packages);
                _packageService
                    .Setup(x => x.FindPackagesById("NuGet.Frameworks", It.IsAny<PackageDeprecationFieldsToInclude>()))
                    .Returns(() => (IReadOnlyCollection<Package>)_registrationB.Packages);
            }

            [Fact]
            public async Task ThrowsWhenPackagesIsNull()
            {
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _target.UpdateListedAsync(null, false));
            }

            [Fact]
            public async Task UsesPointQueryForSingleVersion()
            {
                // Arrange
                var package = _registrationA.Packages.Single(x => x.NormalizedVersion == "4.4.0");
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"))
                    .Returns(package);

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.4.0" }
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                _packageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"),
                    Times.Once);
                _packageService.Verify(
                    x => x.FindPackagesById(It.IsAny<string>(), It.IsAny<PackageDeprecationFieldsToInclude>()),
                    Times.Never);
                Assert.Single(results);
                Assert.Equal(UpdateListedServiceResult.Success, results[0].Result);
            }

            [Fact]
            public async Task UsesBulkQueryForMultipleVersions()
            {
                // Arrange
                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.3.0" },
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.4.0" },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                _packageService.Verify(
                    x => x.FindPackagesById("NuGet.Versioning", PackageDeprecationFieldsToInclude.DeprecationAndRelationships),
                    Times.Once);
                _packageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
                Assert.Equal(2, results.Count);
                Assert.All(results, r => Assert.Equal(UpdateListedServiceResult.Success, r.Result));
            }

            [Fact]
            public async Task GroupsByPackageIdAndCallsUpdatePerGroup()
            {
                // Arrange
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.3.0"))
                    .Returns(_registrationA.Packages.Single(x => x.NormalizedVersion == "4.3.0"));
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Frameworks", "5.3.0"))
                    .Returns(_registrationB.Packages.Single(x => x.NormalizedVersion == "5.3.0"));

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.3.0" },
                    new UpdateListedPackageIdentity { Id = "NuGet.Frameworks", Version = "5.3.0" },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                _packageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(
                        It.Is<IReadOnlyList<Package>>(l => l.All(p => p.PackageRegistration.Id == "NuGet.Versioning")),
                        false, null, null),
                    Times.Once);
                _packageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(
                        It.Is<IReadOnlyList<Package>>(l => l.All(p => p.PackageRegistration.Id == "NuGet.Frameworks")),
                        false, null, null),
                    Times.Once);
                Assert.Equal(2, results.Count);
                Assert.All(results, r => Assert.Equal(UpdateListedServiceResult.Success, r.Result));
            }

            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task FiltersOutIneligiblePackageStatuses(PackageStatus status)
            {
                // Arrange
                _registrationA.Packages.Single(x => x.NormalizedVersion == "4.3.0").PackageStatusKey = status;

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.3.0" },
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.4.0" },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                Assert.Equal(2, results.Count);
                Assert.Equal(UpdateListedServiceResult.PackageNotFound, results.Single(r => r.Version == "4.3.0").Result);
                Assert.Equal(UpdateListedServiceResult.Success, results.Single(r => r.Version == "4.4.0").Result);

                _packageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(
                        It.Is<IReadOnlyList<Package>>(l => l.Count == 1 && l[0].NormalizedVersion == "4.4.0"),
                        false, null, null),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsNotFoundForMissingPackages()
            {
                // Arrange
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "9.9.9"))
                    .Returns((Package)null);

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "9.9.9" },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                Assert.Single(results);
                Assert.Equal(UpdateListedServiceResult.PackageNotFound, results[0].Result);

                _packageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(
                        It.IsAny<IReadOnlyList<Package>>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotSkipPackagesAlreadyMatchingListedState()
            {
                // Arrange
                _registrationA.Packages.Single(x => x.NormalizedVersion == "4.3.0").Listed = false;

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.3.0" },
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.4.0" },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                Assert.Equal(2, results.Count);
                Assert.All(results, r => Assert.Equal(UpdateListedServiceResult.Success, r.Result));

                _packageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(
                        It.Is<IReadOnlyList<Package>>(l => l.Count == 2),
                        false, null, null),
                    Times.Once);
            }

            [Fact]
            public async Task PassesReasonAndCallerIdentityToUpdateService()
            {
                // Arrange
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"))
                    .Returns(_registrationA.Packages.Single(x => x.NormalizedVersion == "4.4.0"));

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.4.0" },
                };

                // Act
                await _target.UpdateListedAsync(input, false, "test reason", "test-caller");

                // Assert
                _packageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(
                        It.IsAny<IReadOnlyList<Package>>(),
                        false,
                        "test reason",
                        "test-caller"),
                    Times.Once);
            }

            [Fact]
            public async Task NormalizesVersions()
            {
                // Arrange
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"))
                    .Returns(_registrationA.Packages.Single(x => x.NormalizedVersion == "4.4.0"));

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "NuGet.Versioning", Version = "4.4.0.0" },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                _packageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"),
                    Times.Once);
                Assert.Single(results);
                Assert.Equal("4.4.0", results[0].Version);
            }

            [Fact]
            public async Task TrimsWhitespaceFromIdAndVersion()
            {
                // Arrange
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"))
                    .Returns(_registrationA.Packages.Single(x => x.NormalizedVersion == "4.4.0"));

                var input = new List<UpdateListedPackageIdentity>
                {
                    new UpdateListedPackageIdentity { Id = "  NuGet.Versioning  ", Version = "  4.4.0  " },
                };

                // Act
                var results = await _target.UpdateListedAsync(input, false);

                // Assert
                _packageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"),
                    Times.Once);
                Assert.Single(results);
                Assert.Equal(UpdateListedServiceResult.Success, results[0].Result);
            }
        }
    }
}
