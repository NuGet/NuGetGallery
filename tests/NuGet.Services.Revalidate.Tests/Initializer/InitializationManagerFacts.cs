// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Initializer
{
    public class InitializationManagerFacts
    {
        public class TheInitializeAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ThrowsIfAlreadyInitialized()
            {
                // Arrange
                _jobState.Setup(s => s.IsInitializedAsync()).ReturnsAsync(true);

                // Act & Assert
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.InitializeAsync());

                Assert.Equal("Attempted to initialize the revalidation job when it is already initialized!", e.Message);
            }

            [Fact]
            public async Task RemovesPreviousRevalidations()
            {
                // Arrange
                _packageFinder.Setup(f => f.FindMicrosoftPackages()).Returns(new HashSet<int>());
                _packageFinder.Setup(f => f.FindPreinstalledPackages(It.IsAny<HashSet<int>>())).Returns(new HashSet<int>());
                _packageFinder.Setup(f => f.FindDependencyPackages(It.IsAny<HashSet<int>>())).Returns(new HashSet<int>());
                _packageFinder.Setup(f => f.FindAllPackages(It.IsAny<HashSet<int>>())).Returns(new HashSet<int>());

                _packageFinder.Setup(f => f.FindPackageRegistrationInformationAsync(It.IsAny<string>(), It.IsAny<HashSet<int>>()))
                    .ReturnsAsync(new List<PackageRegistrationInformation>());

                var firstRemove = true;

                _packageState.Setup(s => s.RemovePackageRevalidationsAsync(1000))
                    .ReturnsAsync(() =>
                    {
                        if (firstRemove)
                        {
                            firstRemove = false;
                            return 1000;
                        }

                        return 1;
                    });

                // Act & assert
                await _target.InitializeAsync();

                _packageState.Verify(s => s.RemovePackageRevalidationsAsync(1000), Times.Exactly(2));
            }

            [Fact]
            public async Task InsertsMicrosoftThenPreinstalledThenDependenciesThenAllOtherPackages()
            {
                // Arrange
                Setup(
                    microsoftPackages: new[]
                    {
                        new Package { PackageRegistrationKey = 4, DownloadCount = 0, NormalizedVersion = "4.0.0", },
                        new Package { PackageRegistrationKey = 5, DownloadCount = 1, NormalizedVersion = "5.0.0", },
                    },
                    preinstalledPackages: new[]
                    {
                        new Package { PackageRegistrationKey = 1, DownloadCount = 1, NormalizedVersion = "1.0.0", },
                    },
                    dependencyPackages: new[]
                    {
                        new Package { PackageRegistrationKey = 2, DownloadCount = 100, NormalizedVersion = "2.0.0", },
                    },
                    remainingPackages: new[]
                    {
                        new Package { PackageRegistrationKey = 3, DownloadCount = 20, NormalizedVersion = "3.0.0", },
                    });

                // Act & assert
                await _target.InitializeAsync();

                _packageState.Verify(
                    s => s.AddPackageRevalidationsAsync(It.IsAny<IReadOnlyList<PackageRevalidation>>()),
                    Times.Exactly(4));

                Assert.Equal(4, _revalidationBatches.Count);
                Assert.Equal(2, _revalidationBatches[0].Count);
                Assert.Single(_revalidationBatches[1]);
                Assert.Single(_revalidationBatches[2]);
                Assert.Single(_revalidationBatches[3]);

                Assert.Equal("5", _revalidationBatches[0][0].PackageId);
                Assert.Equal("5.0.0", _revalidationBatches[0][0].PackageNormalizedVersion);
                Assert.Equal("4", _revalidationBatches[0][1].PackageId);
                Assert.Equal("4.0.0", _revalidationBatches[0][1].PackageNormalizedVersion);

                Assert.Equal("1", _revalidationBatches[1][0].PackageId);
                Assert.Equal("1.0.0", _revalidationBatches[1][0].PackageNormalizedVersion);

                Assert.Equal("2", _revalidationBatches[2][0].PackageId);
                Assert.Equal("2.0.0", _revalidationBatches[2][0].PackageNormalizedVersion);

                Assert.Equal("3", _revalidationBatches[3][0].PackageId);
                Assert.Equal("3.0.0", _revalidationBatches[3][0].PackageNormalizedVersion);
            }

            [Fact]
            public async Task AddsRevalidationsByDescendingOrderOfDownloadCounts()
            {
                // Arrange
                Setup(remainingPackages: new[]
                {
                    new Package { PackageRegistrationKey = 1, DownloadCount = 2, NormalizedVersion = "1.0.0", },
                    new Package { PackageRegistrationKey = 2, DownloadCount = 4, NormalizedVersion = "2.0.0", },
                    new Package { PackageRegistrationKey = 3, DownloadCount = 1, NormalizedVersion = "3.0.0", },
                });

                // Act & assert
                await _target.InitializeAsync();

                _packageState.Verify(
                    s => s.AddPackageRevalidationsAsync(It.IsAny<IReadOnlyList<PackageRevalidation>>()),
                    Times.Once);

                Assert.Single(_revalidationBatches);
                Assert.Equal(3, _revalidationBatches[0].Count);

                var revalidations = _revalidationBatches.First();

                Assert.Equal("2", revalidations[0].PackageId);
                Assert.Equal("2.0.0", revalidations[0].PackageNormalizedVersion);

                Assert.Equal("1", revalidations[1].PackageId);
                Assert.Equal("1.0.0", revalidations[1].PackageNormalizedVersion);

                Assert.Equal("3", revalidations[2].PackageId);
                Assert.Equal("3.0.0", revalidations[2].PackageNormalizedVersion);
            }

            [Fact]
            public async Task InsertsPackageVersionsByDescendingOrder()
            {
                // Arrange
                Setup(dependencyPackages: new[]
                {
                    new Package { PackageRegistrationKey = 1, DownloadCount = 40, NormalizedVersion = "1.0.0", },
                    new Package { PackageRegistrationKey = 1, DownloadCount = 20, NormalizedVersion = "1.13.0", },
                    new Package { PackageRegistrationKey = 2, DownloadCount = 100, NormalizedVersion = "2.0.0", },
                    new Package { PackageRegistrationKey = 2, DownloadCount = 300, NormalizedVersion = "2.1.0", },
                    new Package { PackageRegistrationKey = 3, DownloadCount = 1, NormalizedVersion = "3.0.0", },
                    new Package { PackageRegistrationKey = 3, DownloadCount = 3, NormalizedVersion = "3.0.0-prerelease", },
                    new Package { PackageRegistrationKey = 3, DownloadCount = 1, NormalizedVersion = "3.1.0", },
                });

                // Act & assert
                await _target.InitializeAsync();

                _packageState.Verify(
                    s => s.AddPackageRevalidationsAsync(It.IsAny<IReadOnlyList<PackageRevalidation>>()),
                    Times.Once);

                Assert.Single(_revalidationBatches);
                Assert.Equal(7, _revalidationBatches[0].Count);

                var revalidations = _revalidationBatches.First();

                Assert.Equal("2", revalidations[0].PackageId);
                Assert.Equal("2.1.0", revalidations[0].PackageNormalizedVersion);

                Assert.Equal("2", revalidations[1].PackageId);
                Assert.Equal("2.0.0", revalidations[1].PackageNormalizedVersion);

                Assert.Equal("1", revalidations[2].PackageId);
                Assert.Equal("1.13.0", revalidations[2].PackageNormalizedVersion);

                Assert.Equal("1", revalidations[3].PackageId);
                Assert.Equal("1.0.0", revalidations[3].PackageNormalizedVersion);

                Assert.Equal("3", revalidations[4].PackageId);
                Assert.Equal("3.1.0", revalidations[4].PackageNormalizedVersion);

                Assert.Equal("3", revalidations[5].PackageId);
                Assert.Equal("3.0.0", revalidations[5].PackageNormalizedVersion);

                Assert.Equal("3", revalidations[6].PackageId);
                Assert.Equal("3.0.0-prerelease", revalidations[6].PackageNormalizedVersion);
            }

            [Theory]
            [MemberData(nameof(PartitionsPackagesIntoBatchesOf1000OrLessVersionsData))]
            public async Task PartitionsPackagesIntoBatchesOf1000OrLessVersions(int[] packageVersions, int expectedBatches)
            {
                // Arrange - For each value in the "packageVersions" array, create a package with that many versions.
                var packages = new List<Package>();

                for (var i = 0; i < packageVersions.Length; i++)
                {
                    for (var j = 0; j < packageVersions[i]; j++)
                    {
                        packages.Add(new Package
                        {
                            PackageRegistrationKey = i,
                            DownloadCount = (j == 0) ? packageVersions.Length - i : 0,
                            NormalizedVersion = $"1.{j}.0"
                        });
                    }
                }

                Setup(remainingPackages: packages);

                // Act & assert
                await _target.InitializeAsync();

                _packageState.Verify(
                    s => s.AddPackageRevalidationsAsync(It.IsAny<IReadOnlyList<PackageRevalidation>>()),
                    Times.Exactly(expectedBatches));

                // A scope should be created for each package set. Also, a scope should be created
                // for each batch.
                _scopeFactory.Verify(f => f.CreateScope(), Times.Exactly(4 + expectedBatches));
            }

            public static IEnumerable<object[]> PartitionsPackagesIntoBatchesOf1000OrLessVersionsData()
            {
                yield return new object[]
                {
                    new[] { 1001 },
                    1,
                };

                yield return new object[]
                {
                    new[] { 1, 1001, 1 },
                    3,
                };

                // Should be batched into two batches of 501 items.
                yield return new object[]
                {
                    new[] { 1, 500, 500, 1 },
                    2,
                };

                yield return new object[]
                {
                    new[] { 500, 500 },
                    1,
                };

                yield return new object[]
                {
                    Enumerable.Repeat(1, 1000).ToArray(),
                    1,
                };
            }

            [Fact]
            public async Task MarksAsInitializedAfterAddingRevalidations()
            {
                // Arrange
                Setup(remainingPackages: new[]
                {
                    new Package { PackageRegistrationKey = 3, DownloadCount = 20, NormalizedVersion = "3.0.0", },
                });

                int order = 0;
                int addRevalidationOrder = 0;
                int markAsInitializedOrder = 0;

                _packageState
                    .Setup(s => s.AddPackageRevalidationsAsync(It.IsAny<IReadOnlyList<PackageRevalidation>>()))
                    .Callback(() => addRevalidationOrder = order++)
                    .Returns(Task.CompletedTask);

                _jobState
                    .Setup(s => s.MarkAsInitializedAsync())
                    .Callback(() => markAsInitializedOrder = order++)
                    .Returns(Task.CompletedTask);

                // Act & Assert
                await _target.InitializeAsync();

                _jobState.Verify(s => s.MarkAsInitializedAsync(), Times.Once);

                Assert.True(markAsInitializedOrder > addRevalidationOrder);
            }

            private void Setup(
                IEnumerable<Package> microsoftPackages = null,
                IEnumerable<Package> preinstalledPackages = null,
                IEnumerable<Package> dependencyPackages = null,
                IEnumerable<Package> remainingPackages = null)
            {
                microsoftPackages = microsoftPackages ?? new Package[0];
                preinstalledPackages = preinstalledPackages ?? new Package[0];
                dependencyPackages = dependencyPackages ?? new Package[0];
                remainingPackages = remainingPackages ?? new Package[0];

                HashSet<int> RegistrationKeys(IEnumerable<Package> packages)
                {
                    return new HashSet<int>(packages.Select(p => p.PackageRegistrationKey));
                }

                // Make each set of packages findable.
                _packageFinder
                    .Setup(f => f.FindMicrosoftPackages())
                    .Returns(RegistrationKeys(microsoftPackages));

                _packageFinder
                    .Setup(f => f.FindPreinstalledPackages(It.IsAny<HashSet<int>>()))
                    .Returns(RegistrationKeys(preinstalledPackages));

                _packageFinder
                    .Setup(f => f.FindDependencyPackages(It.IsAny<HashSet<int>>()))
                    .Returns(RegistrationKeys(dependencyPackages));

                _packageFinder
                    .Setup(f => f.FindAllPackages(It.IsAny<HashSet<int>>()))
                    .Returns(RegistrationKeys(remainingPackages));

                // Build the registration information for each set.
                List<PackageRegistrationInformation> RegistrationInformation(IEnumerable<Package> packages)
                {
                    return packages
                        .GroupBy(p => p.PackageRegistrationKey)
                        .Select(g => new PackageRegistrationInformation
                        {
                            Key = g.Key,
                            Id = g.Key.ToString(),
                            Downloads = g.Sum(p => p.DownloadCount),
                            Versions = g.Count()
                        })
                        .ToList();
                }

                _packageFinder
                    .Setup(f => f.FindPackageRegistrationInformationAsync(PackageFinder.MicrosoftSetName, It.IsAny<HashSet<int>>()))
                    .ReturnsAsync(RegistrationInformation(microsoftPackages));

                _packageFinder
                    .Setup(f => f.FindPackageRegistrationInformationAsync(PackageFinder.PreinstalledSetName, It.IsAny<HashSet<int>>()))
                    .ReturnsAsync(RegistrationInformation(preinstalledPackages));

                _packageFinder
                    .Setup(f => f.FindPackageRegistrationInformationAsync(PackageFinder.DependencySetName, It.IsAny<HashSet<int>>()))
                    .ReturnsAsync(RegistrationInformation(dependencyPackages));

                _packageFinder
                    .Setup(f => f.FindPackageRegistrationInformationAsync(PackageFinder.RemainingSetName, It.IsAny<HashSet<int>>()))
                    .ReturnsAsync(RegistrationInformation(remainingPackages));

                // Build the list of versions for each version of packages.
                var versions = microsoftPackages
                    .Concat(preinstalledPackages)
                    .Concat(dependencyPackages)
                    .Concat(remainingPackages)
                    .GroupBy(p => p.PackageRegistrationKey)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(p => NuGetVersion.Parse(p.NormalizedVersion)).ToList());

                _packageFinder.Setup(f => f.FindAppropriateVersions(It.IsAny<List<PackageRegistrationInformation>>()))
                    .Returns<List<PackageRegistrationInformation>>(registrations =>
                    {
                        var result = new Dictionary<int, List<NuGetVersion>>();

                        foreach (var registration in registrations)
                        {
                            result[registration.Key] = versions[registration.Key];
                        }

                        return result;
                    });
            }

            private readonly List<IReadOnlyList<PackageRevalidation>> _revalidationBatches;

            public TheInitializeAsyncMethod()
            {
                _revalidationBatches = new List<IReadOnlyList<PackageRevalidation>>();

                _packageState
                    .Setup(s => s.AddPackageRevalidationsAsync(It.IsAny<IReadOnlyList<PackageRevalidation>>()))
                    .Callback<IReadOnlyList<PackageRevalidation>>(r => _revalidationBatches.Add(r))
                    .Returns(Task.CompletedTask);
            }
        }

        public class TheVerifyAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ThrowsIfNotInitialized()
            {
                _jobState.Setup(s => s.IsInitializedAsync()).ReturnsAsync(false);

                var e = await Assert.ThrowsAsync<Exception>(() => _target.VerifyInitializationAsync());

                Assert.Equal("Expected revalidation state to be initialized", e.Message);
            }

            [Fact]
            public async Task ThrowsIfAppropriatePackageCountDoesNotMatchRevalidationCount()
            {
                _jobState.Setup(s => s.IsInitializedAsync()).ReturnsAsync(true);
                _packageFinder.Setup(f => f.AppropriatePackageCount()).Returns(100);
                _packageState.Setup(s => s.PackageRevalidationCountAsync()).ReturnsAsync(50);

                var e = await Assert.ThrowsAsync<Exception>(() => _target.VerifyInitializationAsync());

                Assert.Equal("Expected 100 revalidation, found 50", e.Message);
            }

            [Fact]
            public async Task DoesNotThrowIfCountsMatch()
            {
                _jobState.Setup(s => s.IsInitializedAsync()).ReturnsAsync(true);
                _packageFinder.Setup(f => f.AppropriatePackageCount()).Returns(100);
                _packageState.Setup(s => s.PackageRevalidationCountAsync()).ReturnsAsync(100);

                await _target.VerifyInitializationAsync();
            }
        }

        public class FactsBase
        {
            public readonly Mock<IRevalidationJobStateService> _jobState;
            public readonly Mock<IPackageRevalidationStateService> _packageState;
            public readonly Mock<IPackageFinder> _packageFinder;
            public readonly Mock<IServiceScopeFactory> _scopeFactory;

            public readonly InitializationConfiguration _config;
            public readonly InitializationManager _target;

            public FactsBase()
            {
                _jobState = new Mock<IRevalidationJobStateService>();
                _packageState = new Mock<IPackageRevalidationStateService>();
                _packageFinder = new Mock<IPackageFinder>();
                _scopeFactory = new Mock<IServiceScopeFactory>();

                var scope = new Mock<IServiceScope>();
                var serviceProvider = new Mock<IServiceProvider>();

                serviceProvider.Setup(p => p.GetService(typeof(IRevalidationJobStateService))).Returns(_jobState.Object);
                serviceProvider.Setup(p => p.GetService(typeof(IPackageRevalidationStateService))).Returns(_packageState.Object);
                serviceProvider.Setup(p => p.GetService(typeof(IPackageFinder))).Returns(_packageFinder.Object);
                serviceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(_scopeFactory.Object);

                scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
                _scopeFactory.Setup(s => s.CreateScope()).Returns(scope.Object);

                _config = new InitializationConfiguration();

                _target = new InitializationManager(
                    _jobState.Object,
                    _packageState.Object,
                    _packageFinder.Object,
                    _scopeFactory.Object,
                    _config,
                    Mock.Of<ILogger<InitializationManager>>());
            }
        }
    }
}
