// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ODataV1FeedControllerFacts
        : ODataFeedControllerFactsBase<ODataV1FeedController>
    {
        [Fact]
        public async Task Get_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V1FeedPackage>(
                (controller, options) => controller.Get(options),
                "/api/v1/Packages");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetInt<V1FeedPackage>(
                (controller, options) => controller.GetCount(options),
                "/api/v1/Packages/$count");

            // Assert
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), count);
        }

        [Fact]
        public async Task FindPackagesById_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V1FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, TestPackageId),
                $"/api/v1/FindPackagesById?id='{TestPackageId}'");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
        }

        [Fact]
        public async Task FindPackagesByIdCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetInt<V1FeedPackage>(
                async (controller, options) => await controller.FindPackagesByIdCount(options, TestPackageId),
                $"/api/v1/FindPackagesById/$count?id='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V1FeedPackage>(
                async (controller, options) => await controller.Search(options, TestPackageId),
                $"/api/v1/Search?searchTerm='{TestPackageId}'");

            // Assert
            AssertSemVer2PackagesFilteredFromResult(resultSet);
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), resultSet.Count);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var searchCount = await GetInt<V1FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, TestPackageId),
                $"/api/v1/Search/$count?searchTerm='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Where(p => !p.IsPrerelease).Count(), searchCount);
        }

        protected override ODataV1FeedController CreateController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService,
            ITelemetryService telemetryService,
            IIconUrlProvider iconUrlProvider)
        {
            return new ODataV1FeedController(
                packagesRepository,
                configurationService,
                searchService,
                telemetryService,
                iconUrlProvider);
        }

        private void AssertSemVer2PackagesFilteredFromResult(IEnumerable<V1FeedPackage> resultSet)
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
    }
}