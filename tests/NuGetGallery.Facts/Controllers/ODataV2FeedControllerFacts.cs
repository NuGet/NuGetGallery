// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ODataV2FeedControllerFacts
        : ODataFeedControllerFactsBase<ODataV2FeedController>
    {
        [Fact]
        public async Task Get_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.Get(options),
                "/api/v2/Packages");

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
                (controller, options) => controller.Get(options, semVerLevel),
                $"/api/v2/Packages?semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet);
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Act
            var count = await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetCount(options),
                "/api/v2/Packages/$count");

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
                (controller, options) => controller.GetCount(options, semVerLevel),
                $"/api/v2/Packages/$count?semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AvailablePackages.Count, count);
        }

        [Fact]
        public async Task FindPackagesById_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, TestPackageId),
                $"/api/v2/FindPackagesById?id='{TestPackageId}'");

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
                (controller, options) => controller.FindPackagesById(options, id: TestPackageId, semVerLevel: semVerLevel),
                $"/api/v2/FindPackagesById?id='{TestPackageId}'&semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet);
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
        }

        [Fact]
        public async Task FindPackagesByIdCount_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Act
            var count = await GetInt<V2FeedPackage>(
                (controller, options) => controller.FindPackagesByIdCount(options, id: TestPackageId),
                $"/api/v2/FindPackagesById/$count?id='{TestPackageId}'");

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
                (controller, options) => controller.FindPackagesByIdCount(options, id: TestPackageId, semVerLevel: semVerLevel),
                $"/api/v2/FindPackagesById/$count?id='{TestPackageId}'&semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AvailablePackages.Count, count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersionsByDefault_IncludePrerelease()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true),
                $"/api/v2/Search?searchTerm='{TestPackageId}'&includePrerelease=true");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersionsByDefault_ExcludePrerelease()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: false),
                $"/api/v2/Search?searchTerm='{TestPackageId}'&includePrerelease=false");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
        }

        [Fact]
        public async Task SearchIsAbsoluteLatestVersion_ReturnsLatestSemVer2_WhenSemVerLevel200()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: SemVerLevelKey.SemVerLevel2),
                $"/api/v2/Search?$filter=IsAbsoluteLatestVersion&searchTerm='{TestPackageId}'&includePrerelease=true&semVerLevel=2.0.0");

            // Assert
            Assert.Equal(SemVer2Packages.Single(p => p.IsLatestSemVer2).Version, resultSet.Single().Version);
        }

        [Fact]
        public async Task SearchIsAbsoluteLatestVersion_ReturnsLatest_WhenHigherPrereleaseAvailableAndIncludePrereleaseTrueAndSemVerLevelUndefined()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: null),
                $"/api/v2/Search?$filter=IsAbsoluteLatestVersion&searchTerm='{TestPackageId}'&includePrerelease=true");

            // Assert
            Assert.Equal(NonSemVer2Packages.Single(p => p.IsLatest).Version, resultSet.Single().Version);
        }

        [Fact]
        public async Task SearchIsLatestVersion_ReturnsLatestStableSemVer2_WhenSemVerLevel200()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: SemVerLevelKey.SemVerLevel2),
                $"/api/v2/Search?$filter=IsLatestVersion&searchTerm='{TestPackageId}'&includePrerelease=true&semVerLevel=2.0.0");

            // Assert
            Assert.Equal(SemVer2Packages.Single(p => p.IsLatestStableSemVer2).Version, resultSet.Single().Version);
        }

        [Fact]
        public async Task SearchIsLatestVersion_ReturnsLatestStable_WhenIncludePrereleaseFalseAndSemVerLevelUndefined()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: false,
                    semVerLevel: null),
                $"/api/v2/Search?$filter=IsLatestVersion&searchTerm='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Single(p => p.IsLatestStable).Version, resultSet.Single().Version);
        }

        [Fact]
        public async Task SearchIsLatestVersion_ReturnsLatestStable_WhenIncludePrereleaseFalseAndSemVerLevel100()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: false,
                    semVerLevel: "1.0.0"),
                $"/api/v2/Search?$filter=IsLatestVersion&searchTerm='{TestPackageId}'&semVerLevel=1.0.0");

            // Assert
            Assert.Equal(NonSemVer2Packages.Single(p => p.IsLatestStable).Version, resultSet.Single().Version);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task Search_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.Search(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: semVerLevel),
                $"/api/v2/Search?searchTerm='{TestPackageId}'?includePrerelease=true&semVerLevel={semVerLevel}");

            // Assert
            AssertSemVer2PackagesIncludedInResult(resultSet);
            Assert.Equal(AvailablePackages.Count, resultSet.Count);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true),
                $"/api/v2/Search/$count?searchTerm='{TestPackageId}'&includePrerelease=true");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, searchCount);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task SearchCount_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(
                    options,
                    searchTerm: TestPackageId,
                    includePrerelease: true,
                    semVerLevel: semVerLevel),
                $"/api/v2/Search/$count?searchTerm='{TestPackageId}'&includePrerelease=true&semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(AvailablePackages.Count, searchCount);
        }

        [Fact]
        public async Task GetUpdates_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Arrange
            const string currentVersionString = "1.0.0";
            var currentVersion = NuGetVersion.Parse(currentVersionString);
            var expected = NonSemVer2Packages.Where(p => NuGetVersion.Parse(p.Version) > currentVersion);

            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.GetUpdates(options, TestPackageId, currentVersionString, includePrerelease: true, includeAllVersions: true),
                $"/api/v2/GetUpdates()?packageIds='{TestPackageId}'&versions='{currentVersionString}'&includePrerelease=true&includeAllVersions=true");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(expected.Count(), resultSet.Count);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task GetUpdates_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Arrange
            const string currentVersionString = "1.0.0";
            var currentVersion = NuGetVersion.Parse(currentVersionString);
            var expected = AvailablePackages.Where(p => NuGetVersion.Parse(p.Version) > currentVersion);

            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.GetUpdates(options, TestPackageId, currentVersionString, includePrerelease: true, includeAllVersions: true, semVerLevel: semVerLevel),
                $"/api/v2/GetUpdates()?packageIds='{TestPackageId}'&versions='{currentVersionString}'&includePrerelease=true&includeAllVersions=true&semVerLevel={semVerLevel}");

            // Assert
            foreach (var package in SemVer2Packages.Where(p => NuGetVersion.Parse(p.Version) > currentVersion))
            {
                // Assert all of the SemVer2 packages are included in the result.
                // Whilst at it, also check the NormalizedVersion on the OData feed.
                Assert.Single(resultSet.Where(feedPackage =>
                            string.Equals(feedPackage.Version, package.Version)
                            && string.Equals(feedPackage.NormalizedVersion, package.NormalizedVersion)
                            && string.Equals(feedPackage.Id, package.PackageRegistration.Id)));
            }

            foreach (var package in NonSemVer2Packages.Where(p => NuGetVersion.Parse(p.Version) > currentVersion))
            {
                // Assert all of the non-SemVer2 packages are included in the result.
                // Whilst at it, also check the NormalizedVersion on the OData feed.
                Assert.Single(resultSet.Where(feedPackage =>
                            string.Equals(feedPackage.Version, package.Version)
                            && string.Equals(feedPackage.NormalizedVersion, package.NormalizedVersion)
                            && string.Equals(feedPackage.Id, package.PackageRegistration.Id)));
            }

            Assert.Equal(expected.Count(), resultSet.Count);
        }

        [Fact]
        public async Task GetUpdatesCount_FiltersSemVerV2PackageVersionsByDefault()
        {
            // Arrange
            const string currentVersionString = "1.0.0";
            var currentVersion = NuGetVersion.Parse(currentVersionString);
            var expected = NonSemVer2Packages.Where(p => NuGetVersion.Parse(p.Version) > currentVersion);

            // Act
            var updatesCount = await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetUpdatesCount(options, TestPackageId, currentVersionString, includePrerelease: true, includeAllVersions: true),
                $"/api/v2/GetUpdates()?packageIds='{TestPackageId}'&versions='{currentVersionString}'&includePrerelease=true&includeAllVersions=true");

            // Assert
            Assert.Equal(expected.Count(), updatesCount);
        }

        [Theory]
        [InlineData("2.0.0")]
        [InlineData("2.0.1")]
        [InlineData("3.0.0-alpha")]
        [InlineData("3.0.0")]
        public async Task GetUpdatesCount_IncludesSemVerV2PackageVersionsWhenSemVerLevel2OrHigher(string semVerLevel)
        {
            // Arrange
            const string currentVersionString = "1.0.0";
            var currentVersion = NuGetVersion.Parse(currentVersionString);
            var expected = AvailablePackages.Where(p => NuGetVersion.Parse(p.Version) > currentVersion);

            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetUpdatesCount(
                    options,
                    packageIds: TestPackageId,
                    versions: currentVersionString,
                    includePrerelease: true,
                    includeAllVersions: true,
                    semVerLevel: semVerLevel),
                $"/api/v2/GetUpdates()?packageIds='{TestPackageId}'&versions='{currentVersionString}'&includePrerelease=true&includeAllVersions=true&semVerLevel={semVerLevel}");

            // Assert
            Assert.Equal(expected.Count(), searchCount);
        }

        protected override ODataV2FeedController CreateController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService,
            ITelemetryService telemetryService,
            IIconUrlProvider iconUrlProvider)
        {
            return new ODataV2FeedController(
                packagesRepository,
                configurationService,
                searchService,
                telemetryService);
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

        private void AssertSemVer2PackagesIncludedInResult(IReadOnlyCollection<V2FeedPackage> resultSet)
        {
            foreach (var package in SemVer2Packages)
            {
                // Assert all of the SemVer2 packages are included in the result.
                // Whilst at it, also check the NormalizedVersion on the OData feed.
                Assert.Single(resultSet.Where(feedPackage =>
                    string.Equals(feedPackage.Version, package.Version)
                    && string.Equals(feedPackage.NormalizedVersion, package.NormalizedVersion)
                    && string.Equals(feedPackage.Id, package.PackageRegistration.Id)));
            }

            foreach (var package in NonSemVer2Packages)
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