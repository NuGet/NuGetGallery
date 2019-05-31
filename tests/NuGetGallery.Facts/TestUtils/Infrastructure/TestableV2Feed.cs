// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Controllers;

namespace NuGetGallery.TestUtils.Infrastructure
{
    public class TestableV2Feed : ODataV2FeedController
    {
        public TestableV2Feed(
            IReadOnlyEntityRepository<Package> repo,
            IGalleryConfigurationService configuration,
            ISearchService searchService)
            : base(repo, Mock.Of<IEntityRepository<Package>>(), configuration, GetNotNullISearchService(searchService), Mock.Of<ITelemetryService>(), GetFeatureFlagService())
        {
        }

        public TestableV2Feed(
            IReadOnlyEntityRepository<Package> repo,
            IGalleryConfigurationService configuration,
            ISearchService searchService,
            ITelemetryService telemetryService)
            : base(repo, Mock.Of<IEntityRepository<Package>>(), configuration, GetNotNullISearchService(searchService), telemetryService, GetFeatureFlagService())
        {
        }

        public string RawUrl { get; set; }

        protected override HttpContextBase GetTraditionalHttpContext()
        {
            if (!string.IsNullOrEmpty(RawUrl))
            {
                return FeedServiceHelpers.GetMockContext(RawUrl.StartsWith("https"), RawUrl);
            }
            return FeedServiceHelpers.GetMockContext();
        }

        public string GetSiteRootForTest()
        {
            return GetSiteRoot();
        }

        private static IFeatureFlagService GetFeatureFlagService()
        {
            var featureFlag = new Mock<IFeatureFlagService>();
            featureFlag.Setup(ff => ff.IsODataDatabaseReadOnlyEnabled()).Returns(true);

            return featureFlag.Object;
        }

        private static ISearchService GetNotNullISearchService(ISearchService searchService)
        {
            return searchService ?? Mock.Of<ISearchService>();
        }
    }
}