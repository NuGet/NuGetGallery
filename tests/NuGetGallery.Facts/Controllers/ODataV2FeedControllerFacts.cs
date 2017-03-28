﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public async Task Get_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                (controller, options) => controller.Get(options),
                "/api/v2/Packages");

            // Assert
            AssertResultCorrect(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count =  await GetInt<V2FeedPackage>(
                (controller, options) => controller.GetCount(options),
                "/api/v2/Packages/$count");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, count);
        }

        [Fact]
        public async Task FindPackagesById_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, TestPackageId),
                $"/api/v2/FindPackagesById?id='{TestPackageId}'");

            // Assert
            AssertResultCorrect(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task Search_FiltersSemVerV2PackageVersions()
        {
            // Act
            var resultSet = await GetCollection<V2FeedPackage>(
                async (controller, options) => await controller.Search(options, TestPackageId),
                $"/api/v2/Search?searchTerm='{TestPackageId}'");

            // Assert
            AssertResultCorrect(resultSet);
            Assert.Equal(NonSemVer2Packages.Count, resultSet.Count);
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var searchCount = await GetInt<V2FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, TestPackageId),
                $"/api/v2/Search/$count?searchTerm='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count, searchCount);
        }
        
        [Fact]
        public async Task GetUpdates_FiltersSemVerV2PackageVersions()
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
            AssertResultCorrect(resultSet);
            Assert.Equal(expected.Count(), resultSet.Count);
        }

        [Fact]
        public async Task GetUpdatesCount_FiltersSemVerV2PackageVersions()
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

        protected override ODataV2FeedController CreateController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService)
        {
            return new ODataV2FeedController(packagesRepository, configurationService, searchService);
        }

        private void AssertResultCorrect(IEnumerable<V2FeedPackage> resultSet)
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