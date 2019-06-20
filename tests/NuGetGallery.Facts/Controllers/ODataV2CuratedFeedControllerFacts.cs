// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData.Query;
using System.Web.Http.Results;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using NuGetGallery.WebApi;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ODataV2CuratedFeedControllerFacts
        : ODataFeedControllerFactsBase<ODataV2CuratedFeedController>
    {
        private const string _curatedFeedName = "dummy";

        public class TheGetMethod
        {
            private readonly Package _curatedFeedPackage;
            private readonly Package _mainFeedPackage;
            private readonly FeatureConfiguration _featureConfiguration;
            private readonly Mock<IGalleryConfigurationService> _config;
            private readonly Mock<IAppConfiguration> _appConfig;
            private readonly Mock<ISearchService> _searchService;
            private readonly Mock<IReadOnlyEntityRepository<Package>> _packages;
            private readonly Mock<ITelemetryService> _telemetryService;
            private readonly Mock<IIconUrlProvider> _iconUrlProvider;
            private readonly ODataV2CuratedFeedController _target;
            private readonly HttpRequestMessage _request;
            private readonly ODataQueryOptions<V2FeedPackage> _options;
            private readonly Mock<IEntityRepository<Package>> _readWritePackages;
            private readonly Mock<IFeatureFlagService> _featureFlagService;

            public TheGetMethod()
            {
                _curatedFeedPackage = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Core",
                    },
                };
                _mainFeedPackage = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Versioning",
                    },
                };
                _featureConfiguration = new FeatureConfiguration();
                _appConfig = new Mock<IAppConfiguration>();

                _config = new Mock<IGalleryConfigurationService>();
                _searchService = new Mock<ISearchService>();
                _packages = new Mock<IReadOnlyEntityRepository<Package>>();
                _telemetryService = new Mock<ITelemetryService>();
                _readWritePackages = new Mock<IEntityRepository<Package>>();
                _featureFlagService = new Mock<IFeatureFlagService>();
                _iconUrlProvider = new Mock<IIconUrlProvider>();

                _config
                    .Setup(x => x.Current)
                    .Returns(() => _appConfig.Object);
                _config
                    .Setup(x => x.Features)
                    .Returns(() => _featureConfiguration);
                _config
                    .Setup(x => x.GetSiteRoot(It.IsAny<bool>()))
                    .Returns(() => _siteRoot);
                _packages
                    .Setup(x => x.GetAll())
                    .Returns(() => new[] { _mainFeedPackage }.AsQueryable());
                _featureFlagService
                    .Setup(ff => ff.IsODataDatabaseReadOnlyEnabled())
                    .Returns(true);

                _target = new ODataV2CuratedFeedController(
                    _config.Object,
                    _searchService.Object,
                    _packages.Object,
                    _readWritePackages.Object,
                    _telemetryService.Object,
                    _featureFlagService.Object);

                _request = new HttpRequestMessage(HttpMethod.Get, $"{_siteRoot}/api/v2/curated-feed/{_curatedFeedName}/Packages");
                _options = new ODataQueryOptions<V2FeedPackage>(CreateODataQueryContext<V2FeedPackage>(), _request);
                AddRequestToController(_request, _target);
            }

            [Fact]
            public async Task RedirectedCuratedFeedQueriesMainFeed()
            {
                _appConfig
                    .Setup(x => x.RedirectedCuratedFeeds)
                    .Returns(() => new[] { _curatedFeedName });

                var actionResult = _target.Get(_options, _curatedFeedName);

                var list = await GetPackageListAsync(actionResult);
                var package = Assert.Single(list);
                Assert.Equal(_mainFeedPackage.PackageRegistration.Id, package.Id);
                _packages.Verify(x => x.GetAll(), Times.Once);
            }

            [Fact]
            public async Task MissingAndRedirectedCuratedFeedQueriesMainFeed()
            {
                _appConfig
                    .Setup(x => x.RedirectedCuratedFeeds)
                    .Returns(() => new[] { _curatedFeedName });

                var actionResult = _target.Get(_options, _curatedFeedName);

                var list = await GetPackageListAsync(actionResult);
                var package = Assert.Single(list);
                Assert.Equal(_mainFeedPackage.PackageRegistration.Id, package.Id);
                _packages.Verify(x => x.GetAll(), Times.Once);
            }

            [Fact]
            public void NonRedirectCuratedFeedReturnsNotFound()
            {
                var actionResult = _target.Get(_options, _curatedFeedName);

                Assert.IsType<NotFoundResult>(actionResult);
                _packages.Verify(x => x.GetAll(), Times.Never);
            }

            private static async Task<List<V2FeedPackage>> GetPackageListAsync(IHttpActionResult actionResult)
            {
                var queryResult = (QueryResult<V2FeedPackage>)actionResult;
                var httpResponseMessage = await queryResult.ExecuteAsync(CancellationToken.None);
                var objectContent = (ObjectContent<IQueryable<V2FeedPackage>>)httpResponseMessage.Content;
                return ((IQueryable<V2FeedPackage>)objectContent.Value).ToList();
            }
        }

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

        [Fact]
        public async Task Get_FiltersOutUnavailablePackages()
        {
            // Arrange
            var semVerLevel = "2.0.0";

            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.Get(options, _curatedFeedName, semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/Packages?semVerLevel={semVerLevel}");

            // Assert
            AssertUnavailablePackagesFilteredFromResult(resultSet);
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
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
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
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
            Assert.Equal(AvailablePackages.Count, count);
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
                $"/api/v2/curated-feed/{_curatedFeedName}/FindPackagesById?id='{TestPackageId}'&semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet, includePrerelease: true);
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
        }

        [Fact]
        public async Task FindPackagesByIdCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesByIdCount(options, _curatedFeedName, TestPackageId),
                $"/api/v2/curated-feed/{_curatedFeedName}/FindPackagesById/$count?id='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task FindPackagesByIdCount_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Act
            var count = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesByIdCount(options, _curatedFeedName, id: TestPackageId, semVerLevel: semVerLevel),
                $"/api/v2/curated-feed/{_curatedFeedName}/FindPackagesById/$count?id='{TestPackageId}'&semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AvailablePackages.Count, count);
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
            Assert.Equal(AvailablePackages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
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
                $"/api/v2/curated-feed/{_curatedFeedName}/Search?searchTerm='{TestPackageId}'&semVerLevel={semVerLevel}&includePrerelease=true");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet, includePrerelease: true);
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
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
            Assert.Equal(AvailablePackages.Count, searchCount);
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
            Assert.Equal(AvailablePackages.Where(p => !p.IsPrerelease).Count(), searchCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestReadOnlyFeatureFlag(bool readOnly)
        {
            var packagesRepositoryMock = new Mock<IReadOnlyEntityRepository<Package>>();
            var readWritePackagesRepositoryMock = new Mock<IEntityRepository<Package>>();
            var configurationService = new Mock<IGalleryConfigurationService>().Object;
            var searchService = new Mock<ISearchService>().Object;
            var telemetryService = new Mock<ITelemetryService>().Object;
            var featureFlagServiceMock = new Mock<IFeatureFlagService>();
            featureFlagServiceMock.Setup(ffs => ffs.IsODataDatabaseReadOnlyEnabled()).Returns(readOnly);

            var testController = new ODataV2CuratedFeedController(
                configurationService,
                searchService,
                packagesRepositoryMock.Object,
                readWritePackagesRepositoryMock.Object,
                telemetryService,
                featureFlagServiceMock.Object);

            var pacakges = testController.GetAll();

            if (readOnly)
            {
                packagesRepositoryMock.Verify(r => r.GetAll(), times: Times.Exactly(1));
                readWritePackagesRepositoryMock.Verify(r => r.GetAll(), times: Times.Never);
            }
            else
            {
                packagesRepositoryMock.Verify(r => r.GetAll(), times: Times.Never);
                readWritePackagesRepositoryMock.Verify(r => r.GetAll(), times: Times.Exactly(1));
            }
        }

        protected override ODataV2CuratedFeedController CreateController(
            IReadOnlyEntityRepository<Package> packagesRepository,
            IEntityRepository<Package> readWritePackagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService,
            ITelemetryService telemetryService,
            IFeatureFlagService featureFlagService,
            IIconUrlProvider iconUrlProvider)
        {
            configurationService.Current.RedirectedCuratedFeeds = new[] { _curatedFeedName };

            return new ODataV2CuratedFeedController(
                configurationService,
                searchService,
                packagesRepository,
                readWritePackagesRepository,
                telemetryService,
                featureFlagService);
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

        private void AssertUnavailablePackagesFilteredFromResult(IEnumerable<V2FeedPackage> resultSet)
        {
            foreach (var feedPackage in resultSet)
            {
                Assert.Empty(UnavailablePackages.Where(p => string.Equals(p.Version, feedPackage.Version)));

                // Assert each of the items in the result set is a non-SemVer v2.0.0 package
                Assert.Single(AvailablePackages.Where(p =>
                    string.Equals(p.Version, feedPackage.Version) &&
                    string.Equals(p.PackageRegistration.Id, feedPackage.Id)));
            }
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