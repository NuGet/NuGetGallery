// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
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
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(SemVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(NonSemVer2Packages.Where(p =>
                    string.Equals(p.Version, feedPackage.Version) &&
                    string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }

            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count =  await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetCount(options, _curatedFeedName),
                $"/api/v2/curated-feed/{_curatedFeedName}/Packages/$count");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, count);
        }

        [Fact]
        public async Task FindPackagesById_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, _curatedFeedName, TestPackageId),
                $"/api/v2/curated-feed/{_curatedFeedName}/FindPackagesById?id='{TestPackageId}'");

            // Assert
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(SemVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(NonSemVer2Packages.Where(p =>
                    string.Equals(p.Version, feedPackage.Version) &&
                    string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }

            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(options, _curatedFeedName, TestPackageId),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search?searchTerm='{TestPackageId}'");

            // Assert
            foreach (var feedPackage in resultSet)
            {
                // Assert none of the items in the result set are SemVer v.2.0.0 packages (checking on original version is enough in this case)
                Assert.Empty(SemVer2Packages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(NonSemVer2Packages.Where(p =>
                    string.Equals(p.Version, feedPackage.Version) &&
                    string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }

            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, _curatedFeedName, TestPackageId),
                $"/api/v2/curated-feed/{_curatedFeedName}/Search/$count?searchTerm='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, searchCount);
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
    }
}