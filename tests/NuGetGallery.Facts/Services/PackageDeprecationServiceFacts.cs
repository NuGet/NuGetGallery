// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageDeprecationServiceFacts
    {
        public class TheUpdateDeprecationMethod : TestContainer
        {
            public static IEnumerable<object[]> ThrowsIfPackagesEmpty_Data => MemberDataHelper.AsDataSet(null, new Package[0]);

            [Theory]
            [MemberData(nameof(ThrowsIfPackagesEmpty_Data))]
            public async Task ThrowsIfPackagesEmpty(IReadOnlyCollection<Package> packages)
            {
                var service = Get<PackageDeprecationService>();

                var user = new User { Key = 1 };
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.UpdateDeprecation(
                        packages,
                        PackageDeprecationStatus.NotDeprecated,
                        alternatePackageRegistration: null,
                        alternatePackage: null,
                        customMessage: null,
                        shouldUnlist: false,
                        user: user));
            }

            [Fact]
            public async Task ThrowsIfPackagesHaveDifferentRegistrations()
            {
                var service = Get<PackageDeprecationService>();

                var packages = new[]
                {
                    new Package { PackageRegistration = new PackageRegistration { Id = "a" } },
                    new Package { PackageRegistration = new PackageRegistration { Id = "b" } },
                };

                var user = new User { Key = 1 };
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.UpdateDeprecation(
                        packages,
                        PackageDeprecationStatus.NotDeprecated,
                        alternatePackageRegistration: null,
                        alternatePackage: null,
                        customMessage: null,
                        shouldUnlist: false,
                        user: user));
            }

            [Fact]
            public async Task DeletesExistingDeprecationsIfStatusNotDeprecated()
            {
                // Arrange
                var registration = new PackageRegistration { Id = "theId" };
                var packageWithDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation1 = new Package
                {
                    PackageRegistration = registration
                };

                var packageWithDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation2 = new Package
                {
                    PackageRegistration = registration
                };

                var packages = new[]
                {
                    packageWithDeprecation1,
                    packageWithoutDeprecation1,
                    packageWithDeprecation2,
                    packageWithoutDeprecation2
                };

                var deprecationRepository = GetMock<IEntityRepository<PackageDeprecation>>();
                var existingDeprecations = packages.Select(p => p.Deprecations.SingleOrDefault()).Where(d => d != null).ToList();
                deprecationRepository
                    .Setup(x => x.DeleteOnCommit(It.Is<IEnumerable<PackageDeprecation>>(d => d.SequenceEqual(existingDeprecations))))
                    .Verifiable();

                deprecationRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Completes()
                    .Verifiable();

                var indexingService = GetMock<IIndexingService>();
                foreach (var package in packages)
                {
                    indexingService
                        .Setup(i => i.UpdatePackage(package))
                        .Verifiable();
                }

                var user = new User { Key = 1 };
                var service = Get<PackageDeprecationService>();

                // Act
                await service.UpdateDeprecation(
                    packages,
                    PackageDeprecationStatus.NotDeprecated,
                    alternatePackageRegistration: null,
                    alternatePackage: null,
                    customMessage: null,
                    shouldUnlist: false,
                    user: user);

                // Assert
                deprecationRepository.Verify();
                indexingService.Verify();

                foreach (var package in packages)
                {
                    Assert.Empty(package.Deprecations);
                }
            }

            public enum PackageLatestState
            {
                Not,
                Latest,
                LatestStable,
                LatestSemVer2,
                LatestStableSemVer2
            }

            public static IEnumerable<object[]> ReplacesExistingDeprecations_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.BooleanDataSet(),
                    MemberDataHelper.EnumDataSet<PackageLatestState>());

            [Theory]
            [MemberData(nameof(ReplacesExistingDeprecations_Data))]
            public async Task ReplacesExistingDeprecations(bool shouldUnlist, PackageLatestState packageLatestState)
            {
                // Arrange
                var registration = new PackageRegistration { Id = "theId" };
                var lastTimestamp = new DateTime(2019, 3, 4);

                var packageWithDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() },
                    LastEdited = lastTimestamp,
                    LastUpdated = lastTimestamp,
                    Listed = true
                };

                switch (packageLatestState)
                {
                    case PackageLatestState.Latest:
                        packageWithDeprecation1.IsLatest = true;
                        break;
                    case PackageLatestState.LatestStable:
                        packageWithDeprecation1.IsLatestStable = true;
                        break;
                    case PackageLatestState.LatestSemVer2:
                        packageWithDeprecation1.IsLatestSemVer2 = true;
                        break;
                    case PackageLatestState.LatestStableSemVer2:
                        packageWithDeprecation1.IsLatestStableSemVer2 = true;
                        break;
                }

                var packageWithoutDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    LastEdited = lastTimestamp,
                    LastUpdated = lastTimestamp,
                    Listed = true
                };

                var packageWithDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    LastEdited = lastTimestamp,
                    LastUpdated = lastTimestamp,
                    Listed = true,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                        }
                    }
                };

                var packageWithoutDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    LastEdited = lastTimestamp,
                    LastUpdated = lastTimestamp,
                    Listed = true
                };

                var packageWithDeprecation3 = new Package
                {
                    PackageRegistration = registration,
                    LastEdited = lastTimestamp,
                    LastUpdated = lastTimestamp,
                    Listed = true,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                        }
                    }
                };

                var packages = new[]
                {
                    packageWithDeprecation1,
                    packageWithoutDeprecation1,
                    packageWithDeprecation2,
                    packageWithoutDeprecation2,
                    packageWithDeprecation3
                };

                var deprecationRepository = GetMock<IEntityRepository<PackageDeprecation>>();
                var packagesWithoutDeprecations = packages.Where(p => p.Deprecations.SingleOrDefault() == null).ToList();
                deprecationRepository
                    .Setup(x => x.InsertOnCommit(
                        It.Is<IEnumerable<PackageDeprecation>>(
                            // The deprecations inserted must be identical to the list of packages without deprecations.
                            i => packagesWithoutDeprecations.SequenceEqual(i.Select(d => d.Package)))))
                    .Verifiable();

                deprecationRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Completes()
                    .Verifiable();

                var packageService = GetMock<IPackageService>();
                if (shouldUnlist && packageLatestState != PackageLatestState.Not)
                {
                    packageService
                        .Setup(p => p.UpdateIsLatestAsync(registration, false))
                        .Returns(Task.CompletedTask)
                        .Verifiable();
                }

                var indexingService = GetMock<IIndexingService>();
                foreach (var package in packages)
                {
                    indexingService
                        .Setup(i => i.UpdatePackage(package))
                        .Verifiable();
                }

                var service = Get<PackageDeprecationService>();

                var status = (PackageDeprecationStatus)99;

                var alternatePackageRegistration = new PackageRegistration();
                var alternatePackage = new Package();

                var customMessage = "message";
                var user = new User { Key = 1 };

                // Act
                await service.UpdateDeprecation(
                    packages,
                    status,
                    alternatePackageRegistration,
                    alternatePackage,
                    customMessage,
                    shouldUnlist,
                    user);

                // Assert
                deprecationRepository.Verify();
                packageService.Verify();
                indexingService.Verify();

                foreach (var package in packages)
                {
                    var deprecation = package.Deprecations.Single();
                    Assert.Equal(status, deprecation.Status);
                    Assert.Equal(alternatePackageRegistration, deprecation.AlternatePackageRegistration);
                    Assert.Equal(alternatePackage, deprecation.AlternatePackage);
                    Assert.Equal(customMessage, deprecation.CustomMessage);
                    Assert.True(lastTimestamp < package.LastEdited);
                    Assert.True(lastTimestamp < package.LastUpdated);
                    Assert.Equal(!shouldUnlist, package.Listed);
                }
            }
        }

        public class TheGetDeprecationByPackageMethod : TestContainer
        {
            [Fact]
            public void GetsDeprecationOfPackage()
            {
                // Arrange
                var key = 190304;
                var package = new Package
                {
                    Key = key
                };

                var differentDeprecation = new PackageDeprecation
                {
                    PackageKey = 9925
                };

                var matchingDeprecation = new PackageDeprecation
                {
                    PackageKey = key
                };

                var repository = GetMock<IEntityRepository<PackageDeprecation>>();
                repository
                    .Setup(x => x.GetAll())
                    .Returns(new[]
                    {
                        differentDeprecation,
                        matchingDeprecation
                    }.AsQueryable());

                // Act
                var deprecation = Get<PackageDeprecationService>()
                    .GetDeprecationByPackage(package);

                // Assert
                Assert.Equal(matchingDeprecation, deprecation);
            }

            [Fact]
            public void ThrowsIfMultipleDeprecationsOfPackage()
            {
                // Arrange
                var key = 190304;
                var package = new Package
                {
                    Key = key
                };

                var matchingDeprecation1 = new PackageDeprecation
                {
                    PackageKey = key
                };

                var matchingDeprecation2 = new PackageDeprecation
                {
                    PackageKey = key
                };

                var repository = GetMock<IEntityRepository<PackageDeprecation>>();
                repository
                    .Setup(x => x.GetAll())
                    .Returns(new[]
                    {
                        matchingDeprecation1,
                        matchingDeprecation2
                    }.AsQueryable());

                // Act / Assert
                Assert.Throws<InvalidOperationException>(
                    () => Get<PackageDeprecationService>().GetDeprecationByPackage(package));
            }
        }
    }
}
