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
    public class AutocompleteDatabasePackageIdsQueryFacts
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
            [Fact]
            public async void ValidPackageIdShouldReturnIdsWhosePackagesAreListed()
            {
                var queryResult = await _packageIdsQuery.Execute("n", null, null);

                var allIdsAreFromPackagesThatAreListed = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    return package.Listed;
                });

                Assert.True(allIdsAreFromPackagesThatAreListed);
            }

            [Fact]
            public async void ValidPackageIdShouldReturnIdsWhosePackageStatusIsAvailable()
            {
                var queryResult = await _packageIdsQuery.Execute("n", null, null);

                var allIdsAreFromPackagesWithPackageStatusAvailable = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    return package.PackageStatusKey == PackageStatus.Available;
                });

                Assert.True(allIdsAreFromPackagesWithPackageStatusAvailable);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("2.0.0")]
            [InlineData("2.0.0-rc.2")]
            [InlineData("2.0.0-rc.1")]
            [InlineData("1.0.0")]
            [InlineData("1.0.0-beta")]
            public async void WithValidSemVerLevelReturnIdsWhosePackagesSemVerLevelCompliant(string semVerLevel)
            {
                var queryResult = await _packageIdsQuery.Execute("nuget", null, semVerLevel);

                var allIdsAreFromPackagesWithSemVerLevelCompliant = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    return SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel).Compile()(package);
                });

                Assert.True(allIdsAreFromPackagesWithSemVerLevelCompliant);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(false)]
            public async void ValidPackageIdWithWithPrereleaseFalseOrNullReturnsIdsWhosePackagePrereleaseIsFalse(bool? includePrerelease)
            {
                var queryResult = await _packageIdsQuery.Execute("nuget", includePrerelease, null);

                var allIdsAreFromPackagesWithPrereleaseFalse = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    return !package.IsPrerelease;
                });

                Assert.True(allIdsAreFromPackagesWithPrereleaseFalse);
            }

            [Fact]
            public async void InexistentPartialIdShouldReturnEmptyArray()
            {
                var queryResult = await _packageIdsQuery.Execute("inexistent-partial-package-id", null, null);

                Assert.Equal(0, queryResult.Count());
            }

            [Fact]
            public async void WithPrereleaseTrueReturnsIdsWThatStartsWithThatPartialId()
            {
                var queryResult = await _packageIdsQuery.Execute("nuget", true, null);

                var allIdsAreFromTheSamePackage = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    return package.PackageRegistration.Id.StartsWith("n");
                });

                Assert.True(allIdsAreFromTheSamePackage);
            }

            [Fact]
            public async void WithValidPartialIdReturnsIdsThatStartsWithThatPartialId()
            {
                var queryResult = await _packageIdsQuery.Execute("n", null, null);

                var allIdsStartsWithPartialId = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    return package.PackageRegistration.Id.StartsWith("n");
                });

                Assert.True(allIdsStartsWithPartialId);
            }

            [Fact]
            public async void WithValidPartialIdReturnsIdsThatAreOrderedById()
            {
                var queryResult = await _packageIdsQuery.Execute("n", null, null);

                var previousId = "";
                var allIdsAreOrderedById = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    var comparisonResult = previousId.CompareTo(package.PackageRegistration.Id);
                    previousId = package.PackageRegistration.Id;
                    return comparisonResult <= 0;
                });

                Assert.True(allIdsAreOrderedById);
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async void WithNoPartialIdReturnsIdsThatAreOrderedDescByMaxDownloadCount(string partialId)
            {
                var queryResult = await _packageIdsQuery.Execute(partialId, null, null);

                var maxValue = Int32.MaxValue;
                var allIdsAreOrderedById = queryResult.All(id =>
                {
                    _packageDictionary.TryGetValue(id, out var package);
                    var comparisonResult = maxValue >= package.PackageRegistration.DownloadCount;
                    maxValue = package.PackageRegistration.DownloadCount;
                    return comparisonResult;
                });

                Assert.True(allIdsAreOrderedById);
            }

            [Theory]
            [MemberData(nameof(ConstructorData))]
            public async void Returns30IdsAtMost(IList<Package> packages)
            {
                _packageRepository
                    .Setup(context => context.GetAll())
                    .Returns(packages.AsQueryable());

                var queryResult = await _packageIdsQuery.Execute("nuget", null, null);

                Assert.True(queryResult.Count() <= 30);
            }
        }

        public class FactBase
        {
            protected readonly AutocompleteDatabasePackageIdsQuery _packageIdsQuery;
            protected readonly Mock<IReadOnlyEntityRepository<Package>> _packageRepository;
            protected readonly IDictionary<string, Package> _packageDictionary;

            public FactBase()
            {
                _packageRepository = new Mock<IReadOnlyEntityRepository<Package>>();

                var packages = CreatePackages();

                var packagesGrouped = packages.GroupBy(p => p.PackageRegistration.Id);
                _packageDictionary = packagesGrouped
                    .Select(group => new
                    {
                        Id = group.Key,
                        Package = group.OrderByDescending(package => package.PackageRegistration.DownloadCount).First()
                    })
                    .OrderByDescending(group => group.Package.DownloadCount)
                    .ToDictionary(p => p.Id, p => p.Package);

                _packageRepository
                    .Setup(context => context.GetAll())
                    .Returns(packages.AsQueryable());

                _packageIdsQuery = new AutocompleteDatabasePackageIdsQuery(_packageRepository.Object);
            }

            public static IEnumerable<object[]> ConstructorData()
            {
                var packages = new List<Package>();
                for (var i = 0; i < 40; i++)
                {
                    var package = new Package()
                    {
                        PackageRegistration = new PackageRegistration()
                        {
                            Id = "nuget-" + i,
                            DownloadCount = 1
                        },
                        PackageStatusKey = PackageStatus.Available,
                        SemVerLevelKey = null,
                        IsPrerelease = true,
                        Version = "1.0.0"
                    };
                    packages.Add(package);
                }

                return new List<object[]>
                {
                    new object[] { packages.GetRange(0, 10)},
                    new object[] { packages.GetRange(0, 30)},
                    new object[] { packages.GetRange(0, 40)}
                };
            }

            private IReadOnlyList<Package> CreatePackages()
            {
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget",
                        DownloadCount = 1
                    },
                    PackageStatusKey = PackageStatus.Available,
                    SemVerLevelKey = null,
                    IsPrerelease = true,
                    Version = "1.0.0"
                };
                var packageUnlisted = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget",
                        DownloadCount = 2
                    },
                    SemVerLevelKey = null,
                    IsPrerelease = false,
                    Version = "2.0.0"
                };
                var packageListedWithStatusValidating = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget-1",
                        DownloadCount = 4
                    },
                    SemVerLevelKey = null,
                    IsPrerelease = false,
                    Version = "3.0.0"
                };
                var packageDifferentId = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget-2",
                        DownloadCount = 1
                    },
                    SemVerLevelKey = null,
                    IsPrerelease = false,
                    Version = "4.0.0"
                };
                var packageSemVerLevel2 = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget-3",
                        DownloadCount = 1
                    },
                    SemVerLevelKey = 2,
                    IsPrerelease = false,
                    Version = "5.0.0"
                };
                var packageSemVerLevel3 = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget-4",
                        DownloadCount = 1
                    },
                    SemVerLevelKey = 3,
                    IsPrerelease = false,
                    Version = "6.0.0"
                };
                var packagePrerelease = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "nuget-5",
                        DownloadCount = 1
                    },
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
