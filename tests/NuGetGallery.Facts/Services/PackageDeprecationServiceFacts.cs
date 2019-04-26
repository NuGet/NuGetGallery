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

                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.UpdateDeprecation(
                        packages,
                        PackageDeprecationStatus.NotDeprecated,
                        null,
                        null,
                        null));
            }

            [Fact]
            public async Task DeletesExistingDeprecationsIfStatusNotDeprecated()
            {
                // Arrange
                var packageWithDeprecation1 = new Package
                {
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation1 = new Package();

                var packageWithDeprecation2 = new Package
                {
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation2 = new Package();

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

                var service = Get<PackageDeprecationService>();

                // Act
                await service.UpdateDeprecation(
                    packages,
                    PackageDeprecationStatus.NotDeprecated,
                    null,
                    null,
                    null);

                // Assert
                deprecationRepository.Verify();

                foreach (var package in packages)
                {
                    Assert.Empty(package.Deprecations);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReplacesExistingDeprecations(bool shouldUnlist)
            {
                // Arrange
                var lastEdited = new DateTime(2019, 3, 4);

                var packageWithDeprecation1 = new Package
                {
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() },
                    LastEdited = lastEdited
                };

                var packageWithoutDeprecation1 = new Package
                {
                    LastEdited = lastEdited
                };

                var packageWithDeprecation2 = new Package
                {
                    LastEdited = lastEdited,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                        }
                    }
                };

                var packageWithoutDeprecation2 = new Package
                {
                    LastEdited = lastEdited
                };

                var packageWithDeprecation3 = new Package
                {
                    LastEdited = lastEdited,
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

                var service = Get<PackageDeprecationService>();

                var status = (PackageDeprecationStatus)99;

                var alternatePackageRegistration = new PackageRegistration();
                var alternatePackage = new Package();

                var customMessage = "message";

                // Act
                await service.UpdateDeprecation(
                    packages,
                    status,
                    alternatePackageRegistration,
                    alternatePackage,
                    customMessage);

                // Assert
                deprecationRepository.Verify();

                foreach (var package in packages)
                {
                    var deprecation = package.Deprecations.Single();
                    Assert.Equal(status, deprecation.Status);
                    Assert.Equal(alternatePackageRegistration, deprecation.AlternatePackageRegistration);
                    Assert.Equal(alternatePackage, deprecation.AlternatePackage);
                    Assert.Equal(customMessage, deprecation.CustomMessage);
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
