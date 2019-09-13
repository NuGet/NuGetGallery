// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Queries
{
    public class AutocompleteDatabasePackageVersionsQueryFacts
    {
        public class Constructor
        {
            [Fact]
            public void InvalidArgumentsThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() => new AutocompleteDatabasePackageIdsQuery(null));
            }
        }

        public class Execute : FactBase
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void InvalidIdThrowsArgumentNullException(string id)
            {
                Assert.ThrowsAsync<ArgumentNullException>(() => _packageVersionsQuery.Execute(id, null, null));
            }

            [Fact]
            public async void OnlyReturnsVersionsOfTheSamePackage()
            {
                var queryResult = await _packageVersionsQuery.Execute("nuget", null, null);

                var allVersionsAreFromTheSamePackage = queryResult.All(version =>
                {
                    _packageDictionary.TryGetValue(version, out var package);
                    return package.PackageRegistration.Id == "nuget";
                });

                Assert.True(allVersionsAreFromTheSamePackage);
            }

            [Fact]
            public async void InexistentIdShouldReturnEmptyVersionArray()
            {
                var queryResult = await _packageVersionsQuery.Execute("inexistent-package-id", null, null);

                Assert.Equal(0, queryResult.Count());
            }

            [Fact]
            public async void ValidPackageIdShouldReturnVersionsWhosePackageStatusIsAvailable()
            {
                var queryResult = await _packageVersionsQuery.Execute("nuget", null, null);

                var allVersionsAreFromPackagesWithPackageStatusAvailable = queryResult.All(version =>
                {
                    _packageDictionary.TryGetValue(version, out var package);
                    return package.PackageStatusKey == PackageStatus.Available;
                });

                Assert.True(allVersionsAreFromPackagesWithPackageStatusAvailable);
            }

            [Fact]
            public async void ValidPackageIdShouldReturnVersionsWhosePackagesAreListed()
            {
                var queryResult = await _packageVersionsQuery.Execute("nuget", null, null);

                var allVersionsAreFromPackagesThatAreListed = queryResult.All(version => 
                {
                    _packageDictionary.TryGetValue(version, out var package);
                    return package.Listed;
                });

                Assert.True(allVersionsAreFromPackagesThatAreListed);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("2.0.0")]
            [InlineData("2.0.0-rc.2")]
            [InlineData("2.0.0-rc.1")]
            [InlineData("1.0.0")]
            [InlineData("1.0.0-beta")]
            public async void ValidPackageIdWithSemVerLevelReturnVersionsWhosePackagesHaveSemVerLevelCompliant(string semVerLevel)
            {
                var queryResult = await _packageVersionsQuery.Execute("nuget", null, semVerLevel);
                
                var allVersionsAreFromPackagesWithSemVerLevelCompliant = queryResult.All(version =>
                {
                    _packageDictionary.TryGetValue(version, out var package);
                    return SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel).Compile()(package);
                });

                Assert.True(allVersionsAreFromPackagesWithSemVerLevelCompliant);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(false)]
            public async void ValidPackageIdWithWithPrereleaseFalseOrNullReturnsVersionsWhosePackagePrereleaseIsFalse(bool? includePrerelease)
            {
                var queryResult = await _packageVersionsQuery.Execute("nuget", includePrerelease, null);

                var allVersionsAreFromPackagesWithPrereleaseFalse = queryResult.All(version =>
                {
                    _packageDictionary.TryGetValue(version, out var package);
                    return !package.IsPrerelease;
                });

                Assert.True(allVersionsAreFromPackagesWithPrereleaseFalse);
            }

            [Fact]
            public async void WithPrereleaseTrueReturnsAllVersionsOfTheSamePackage()
            {
                var queryResult = await _packageVersionsQuery.Execute("nuget", true, null);

                var allVersionsAreFromTheSamePackage = queryResult.All(version =>
                {
                    _packageDictionary.TryGetValue(version, out var package);
                    return package.PackageRegistration.Id == "nuget";
                });

                Assert.True(allVersionsAreFromTheSamePackage);
            }
        }

        public class FactBase
        {
            protected readonly AutocompleteDatabasePackageVersionsQuery _packageVersionsQuery;
            protected readonly Mock<IReadOnlyEntityRepository<Package>> _packageRepository;
            protected readonly IDictionary<string, Package> _packageDictionary;
        
            public FactBase()
            {
                _packageRepository = new Mock<IReadOnlyEntityRepository<Package>>();

                var packages = CreatePackages();
                _packageDictionary = packages.ToDictionary(p => p.Version, p => p);

                _packageRepository
                    .Setup(context => context.GetAll())
                    .Returns(packages.AsQueryable());

                _packageVersionsQuery = new AutocompleteDatabasePackageVersionsQuery(_packageRepository.Object);
            }

            private IReadOnlyList<Package> CreatePackages()
            {
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget"
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Listed = true,
                    SemVerLevelKey = null,
                    IsPrerelease = true,
                    Version = "1.0.0"
                };
                var packageUnlisted = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget"
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Listed = false,
                    SemVerLevelKey = null,
                    IsPrerelease = false,
                    Version = "2.0.0"
                };
                var packageListedWithStatusValidating = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget"
                    },
                    PackageStatusKey = PackageStatus.Validating,
                    Listed = true,
                    SemVerLevelKey = null,
                    IsPrerelease = false,
                    Version = "3.0.0"
                };
                var packageDifferentId = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget-server"
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Listed = true,
                    SemVerLevelKey = null,
                    IsPrerelease = false,
                    Version = "4.0.0"
                };
                var packageSemVerLevel2 = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget"
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Listed = true,
                    SemVerLevelKey = 2,
                    IsPrerelease = false,
                    Version = "5.0.0"
                };
                var packageSemVerLevel3 = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget"
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Listed = true,
                    SemVerLevelKey = 3,
                    IsPrerelease = false,
                    Version = "6.0.0"
                };
                var packagePrerelease = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget"
                    },
                    PackageStatusKey = PackageStatus.Available,
                    Listed = true,
                    SemVerLevelKey = null,
                    IsPrerelease = true,
                    Version = "7.0.0-beta"
                };

                var packages = new List<Package>();
                packages.Add(package);
                packages.Add(packageUnlisted);
                packages.Add(packageListedWithStatusValidating);
                packages.Add(packageDifferentId);
                packages.Add(packagePrerelease);
                packages.Add(packageSemVerLevel2);
                packages.Add(packageSemVerLevel3);

                return packages;
            }
        }
    }
}
