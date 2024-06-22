// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http.Results;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using Moq;
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
            Assert.Equal(NonSemVer2Packages.Count(p => !p.IsPrerelease), resultSet.Count);
        }

        [Fact]
        public void Get_ReturnsBadRequestWhenOrderByInvalidColumn()
        {
            // Act
            var resultSet = GetActionResult<V1FeedPackage>(
                (controller, options) => controller.Get(options),
                "/api/v1/Packages?$orderby=abc");

            // Assert
            Assert.IsType<BadRequestErrorMessageResult>(resultSet);
        }

        [Fact]
        public async Task GetAll_ReturnsBadRequestWhenGetAllIsDisabled()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1GetAllEnabled()).Returns(false);

            // Act
            var resultSet = GetActionResult<V1FeedPackage>(
                (controller, options) => controller.Get(options),
                "/api/v1/Packages",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataDisabled);
            featureFlagService.Verify(x => x.IsODataV1GetAllEnabled());
        }

        [Fact]
        public async Task GetAllCount_ReturnsBadRequestWhenGetAllIsDisabled()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1GetAllCountEnabled()).Returns(false);

            // Act
            var resultSet = GetActionResult<V1FeedPackage>(
                (controller, options) => controller.GetCount(options),
                "/api/v1/Packages/$count",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataDisabled);
            featureFlagService.Verify(x => x.IsODataV1GetAllCountEnabled());
        }

        [Fact]
        public async Task GetSpecific_ReturnsBadRequestNonHijackedIsDisabledAndQueryCannotBeHijacked()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1GetSpecificNonHijackedEnabled()).Returns(false);

            // Act
            var resultSet = await GetActionResultAsync<V1FeedPackage>(
                async (controller, options) => await controller.Get(options, TestPackageId, "1.0.0"),
                $"/api/v1/Packages(Id='{TestPackageId}',Version='1.0.0')?$filter=1 eq 1",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataParametersDisabled);
            featureFlagService.Verify(x => x.IsODataV1GetSpecificNonHijackedEnabled());
        }

        [Fact]
        public async Task GetCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetInt<V1FeedPackage>(
                (controller, options) => controller.GetCount(options),
                "/api/v1/Packages/$count");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count(p => !p.IsPrerelease), count);
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
            Assert.Equal(NonSemVer2Packages.Count(p => !p.IsPrerelease), resultSet.Count);
        }

        [Fact]
        public async Task FindPackagesById_ReturnsBadRequestNonHijackedIsDisabledAndQueryCannotBeHijacked()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1FindPackagesByIdNonHijackedEnabled()).Returns(false);

            // Act
            var resultSet = await GetActionResultAsync<V1FeedPackage>(
                async (controller, options) => await controller.FindPackagesById(options, TestPackageId),
                $"/api/v1/FindPackagesById?id='{TestPackageId}'&$orderby=Version",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataParametersDisabled);
            featureFlagService.Verify(x => x.IsODataV1FindPackagesByIdNonHijackedEnabled());
        }

        [Fact]
        public async Task FindPackagesByIdCount_ReturnsBadRequestNonHijackedIsDisabledAndQueryCannotBeHijacked()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1FindPackagesByIdCountNonHijackedEnabled()).Returns(false);

            // Act
            var resultSet = await GetActionResultAsync<V1FeedPackage>(
                async (controller, options) => await controller.FindPackagesByIdCount(options, TestPackageId),
                $"/api/v1/FindPackagesById/$count?id='{TestPackageId}'&$orderby=Version",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataParametersDisabled);
            featureFlagService.Verify(x => x.IsODataV1FindPackagesByIdCountNonHijackedEnabled());
        }

        [Fact]
        public async Task FindPackagesByIdCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var count = await GetInt<V1FeedPackage>(
                async (controller, options) => await controller.FindPackagesByIdCount(options, TestPackageId),
                $"/api/v1/FindPackagesById/$count?id='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count(p => !p.IsPrerelease), count);
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
            Assert.Equal(NonSemVer2Packages.Count(p => !p.IsPrerelease), resultSet.Count);
        }

        [Fact]
        public async Task Search_ReturnsBadRequestNonHijackedIsDisabledAndQueryCannotBeHijacked()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1SearchNonHijackedEnabled()).Returns(false);

            // Act
            var resultSet = await GetActionResultAsync<V1FeedPackage>(
                async (controller, options) => await controller.Search(options, TestPackageId),
                $"/api/v1/Search?searchTerm='{TestPackageId}'&$orderby=Version",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataParametersDisabled);
            featureFlagService.Verify(x => x.IsODataV1SearchNonHijackedEnabled());
        }

        [Fact]
        public async Task SearchCount_ReturnsBadRequestNonHijackedIsDisabledAndQueryCannotBeHijacked()
        {
            // Arrange
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.IsODataV1SearchCountNonHijackedEnabled()).Returns(false);

            // Act
            var resultSet = await GetActionResultAsync<V1FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, TestPackageId),
                $"/api/v1/Search/$count?searchTerm='{TestPackageId}'&$orderby=Version",
                featureFlagService);

            // Assert
            await VerifyODataDeprecation(resultSet, Strings.ODataParametersDisabled);
            featureFlagService.Verify(x => x.IsODataV1SearchCountNonHijackedEnabled());
        }

        [Fact]
        public async Task SearchCount_FiltersSemVerV2PackageVersions()
        {
            // Act
            var searchCount = await GetInt<V1FeedPackage>(
                async (controller, options) => await controller.SearchCount(options, TestPackageId),
                $"/api/v1/Search/$count?searchTerm='{TestPackageId}'");

            // Assert
            Assert.Equal(NonSemVer2Packages.Count(p => !p.IsPrerelease), searchCount);
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

            var searchServiceFactory = new Mock<IHijackSearchServiceFactory>();
            searchServiceFactory
                .Setup(f => f.GetService())
                .Returns(searchService);

            var testController = new ODataV1FeedController(
                packagesRepositoryMock.Object,
                readWritePackagesRepositoryMock.Object,
                configurationService,
                searchServiceFactory.Object,
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

        protected override ODataV1FeedController CreateController(
            IReadOnlyEntityRepository<Package> packagesRepository,
            IEntityRepository<Package> readWritePackagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService,
            ITelemetryService telemetryService,
            IFeatureFlagService featureFlagService)
        {
            var searchServiceFactory = new Mock<IHijackSearchServiceFactory>();
            searchServiceFactory
                .Setup(f => f.GetService())
                .Returns(searchService);

            return new ODataV1FeedController(
                packagesRepository,
                readWritePackagesRepository,
                configurationService,
                searchServiceFactory.Object,
                telemetryService,
                featureFlagService);
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