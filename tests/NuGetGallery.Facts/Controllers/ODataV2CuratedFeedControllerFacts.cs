// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ODataV2CuratedFeedControllerFacts
        : ODataFeedControllerFactsBase<ODataV2CuratedFeedController>
    {
        private const string _curatedFeedName = "dummy";

        [Fact]
        public async Task Get_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.Get(options, _curatedFeedName),
                $"/api/v2/curated-feed/{_curatedFeedName}/Packages");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task Get_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.Get(options, _curatedFeedName, semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Packages?semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet, includePrerelease: true);
            Assert.Equal(AllPackages.Count(), resultSet.Count);
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetCount(options, _curatedFeedName),
                $"/api/v2/curated-feed/{_curatedFeedName}/Packages/$count");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task GetCount_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Act
            var count = await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetCount(options, _curatedFeedName, semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Packages/$count?semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AllPackages.Count(), count);
        }

        [Fact]
        public async Task FindPackagesById_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, _curatedFeedName, TestPackageId),
                $"/api/v2/curated-feed/{_curatedFeedName}/FindPackagesById?id='{TestPackageId}'");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task FindPackagesById_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, _curatedFeedName, id: TestPackageId, semVerLevel: semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/FindPackagesById?id='{TestPackageId}'?semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet, includePrerelease: true);
            Assert.Equal(AllPackages.Count(), resultSet.Count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersions_ExcludePrerelease()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(options, _curatedFeedName, searchTerm: TestPackageId),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search?searchTerm='{TestPackageId}'");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersions_IncludePrerelease()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: true),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search?searchTerm='{TestPackageId}'&includePrerelease=true");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task Search_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher_ExcludePrerelease(string semVerLevel)
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(options, _curatedFeedName, searchTerm: TestPackageId, semVerLevel: semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search?searchTerm='{TestPackageId}'&semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet, includePrerelease: false);
            Assert.Equal(AllPackages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task Search_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher_IncludePrerelease(string semVerLevel)
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search?searchTerm='{TestPackageId}'?semVerLevel={semVerLevel}&includePrerelease=true");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet, includePrerelease: true);
            Assert.Equal(AllPackages.Count(), resultSet.Count);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions_IncludePrerelease()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: true),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'&includePrerelease=true");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, searchCount);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions_ExcludePrerelease()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: false),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), searchCount);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersionsWhenSemVerLevelLowerThan200_IncludePrerelease()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: "1.0.0"),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'&includePrerelease=true&semVerLevel=1.0.0");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, searchCount);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersionsWhenSemVerLevelLowerThan200_ExcludePrerelease()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: false,
                    semVerLevel: "1.0.0"),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'&semVerLevel=1.0.0");

            // Assert
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), searchCount);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task SearchCount_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher_IncludePrerelease(string semVerLevel)
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    _curatedFeedName,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'&includePrerelease=true&semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AllPackages.Count(), searchCount);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task SearchCount_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher_ExcludePrerelease(string semVerLevel)
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, _curatedFeedName, searchTerm: TestPackageId, semVerLevel: semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'&semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AllPackages.Where(p => !p.IsPrerelease).Count(), searchCount);
        }

        protected override ODataV2CuratedFeedController CreateController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService)
        {
            var curatedFeed = new CuratedFeed { Name = _curatedFeedName };

            var curatedFeedServiceMock = new Mock<ICuratedFeedService>(MockBehavior.Strict);
            curatedFeedServiceMock.Setup(m => m.GetPackages(_curatedFeedName)).Returns(AllPackages);
            curatedFeedServiceMock.Setup(m => m.GetFeedByName(_curatedFeedName, false)).Returns(curatedFeed);

            var entitiesContextMock = new Mock<IEntitiesContext>(MockBehavior.Strict);
            var curatedFeedDbSet = GetQueryableMockDbSet(curatedFeed);
            entitiesContextMock.SetupGet(m => m.CuratedFeeds).Returns(curatedFeedDbSet);

            return new ODataV2CuratedFeedController(
                entitiesContextMock.Object,
                configurationService,
                searchService,
                curatedFeedServiceMock.Object);
        }

        private static IDbSet<T> GetQueryableMockDbSet<T>(params T[] sourceList) where T : class
        {
            var queryable = sourceList.AsQueryable();

            var dbSet = new Mock<IDbSet<T>>();
            dbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            dbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            dbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            dbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

            return dbSet.Object;
        }

        private void AssertSemVer2PackagesFilteredFromResult(IEnumerable<V2FeedPackage> resultSet)
        {
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(SemVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(NonSemVer2Packages.Where(p =>
                    string.Equals(p.Version, feedPackage.Version) &&
                    string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }
        }

        private void AssertSemVer2PackagesIncludedInResult(IReadOnlyCollection<V2FeedPackage> resultSet, bool includePrerelease)
        {
            foreach (var package in SemVer2Packages.Where(p => p.IsPrerelease == includePrerelease))
            {
                // Assert all of the SemVer2 packages are included in the result.
                // Whilst at it, also check the NormalizedVersion on the OData feed.
                Assert.Single(resultSet.Where(feedPackage =>
                    string.Equals(feedPackage.Version, package.Version)
                    && string.Equals(feedPackage.NormalizedVersion, package.NormalizedVersion)
                    && string.Equals(feedPackage.Id, package.PackageRegistration.Id)));
            }

            foreach (var package in NonSemVer2Packages.Where(p => p.IsPrerelease == includePrerelease))
            {
                // Assert all of the non-SemVer2 packages are included in the result.
                // Whilst at it, also check the NormalizedVersion on the OData feed.
                Assert.Single(resultSet.Where(feedPackage =>
                    string.Equals(feedPackage.Version, package.Version)
                    && string.Equals(feedPackage.NormalizedVersion, package.NormalizedVersion)
                    && string.Equals(feedPackage.Id, package.PackageRegistration.Id)));
            }
        }
    }
}