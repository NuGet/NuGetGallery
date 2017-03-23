// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.OData;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ODataV1FeedControllerFacts
            : ODataV1ControllerFactsBase
    {
        private const string _testPackageId = "Some.Awesome.Package";
        private readonly IReadOnlyCollection<Package> _nonSemVer2Packages;
        private readonly IReadOnlyCollection<Package> _semVer2Packages;
        private readonly IEntityRepository<Package> _packagesRepository;

        public ODataV1FeedControllerFacts()
        {
            // Arrange
            var packagesQueryable = CreatePackagesQueryable();
            _nonSemVer2Packages = packagesQueryable.Where(p => p.SemVerLevelKey == SemVerLevelKey.Unknown).ToList();
            _semVer2Packages = packagesQueryable.Where(p => p.SemVerLevelKey == SemVerLevelKey.SemVer2).ToList();


            var packagesRepositoryMock = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            packagesRepositoryMock.Setup(m => m.GetAll()).Returns(packagesQueryable).Verifiable();
            _packagesRepository = packagesRepositoryMock.Object;
        }

        [Fact]
        public async Task Get_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetODataV1FeedCollection<V1FeedPackage>(
                (controller, options) => controller.Get(options),
                "/api/v1/Packages",
                _packagesRepository);

            // Assert
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(_semVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(_nonSemVer2Packages.Where(p =>
                            string.Equals(p.Version, feedPackage.Version) &&
                            string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }

            Assert.Equal(_nonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetODataV1FeedCount<V1FeedPackage>(
                (controller, options) => controller.GetCount(options),
                "/api/v1/Packages/$count",
                _packagesRepository);

            // Assert
            Assert.Equal(_nonSemVer2Packages.Count, count);
        }

        [Fact]
        public async Task FindPackagesById_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetODataV1FeedCollectionAsync<V1FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, _testPackageId),
                $"/api/v1/FindPackagesById?id='{_testPackageId}'",
                _packagesRepository);

            // Assert
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(_semVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(_nonSemVer2Packages.Where(p =>
                            string.Equals(p.Version, feedPackage.Version) &&
                            string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }

            Assert.Equal(_nonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetODataV1FeedCollectionAsync<V1FeedPackage>(
                async (controller, options) => await controller.Search(options, _testPackageId),
                $"/api/v1/Search?searchTerm='{_testPackageId}'",
                _packagesRepository);

            // Assert
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(_semVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(_nonSemVer2Packages.Where(p =>
                            string.Equals(p.Version, feedPackage.Version) &&
                            string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }

            Assert.Equal(_nonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var searchCount = await GetODataV1FeedCountAsync<V1FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, _testPackageId),
                $"/api/v1/Search/$count?searchTerm='{_testPackageId}'",
                _packagesRepository);

            // Assert
            Assert.Equal(_nonSemVer2Packages.Count, searchCount);
        }

        private static IQueryable<Package> CreatePackagesQueryable()
        {
            var packageRegistration = new PackageRegistration { Id = _testPackageId };

            var list = new List<Package>
            {
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0.0",
                    NormalizedVersion = "1.0.0.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0.0-alpha",
                    NormalizedVersion = "1.0.0.0-alpha",
                    SemVerLevelKey = SemVerLevelKey.Unknown
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0-alpha",
                    NormalizedVersion = "2.0.0-alpha",
                    SemVerLevelKey = SemVerLevelKey.SemVer2
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0-alpha.1",
                    NormalizedVersion = "2.0.0-alpha.1",
                    SemVerLevelKey = SemVerLevelKey.SemVer2
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0+metadata",
                    NormalizedVersion = "2.0.0",
                    SemVerLevelKey = SemVerLevelKey.SemVer2
                }
            };

            return list.AsQueryable();
        }
    }
}