// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Controllers;

namespace NuGetGallery.TestUtils.Infrastructure
{
    public class TestableV1Feed : ODataV1FeedController
    {
        public TestableV1Feed(
            IEntityRepository<Package> repo,
            IGalleryConfigurationService configuration,
            ISearchService searchService)
            : base(repo, configuration, searchService, Mock.Of<ITelemetryService>(), Mock.Of<IIconUrlProvider>())
        {
        }

        public TestableV1Feed(
            IEntityRepository<Package> repo,
            IGalleryConfigurationService configuration,
            ISearchService searchService,
            ITelemetryService telemetryService)
            : base(repo, configuration, searchService, telemetryService, Mock.Of<IIconUrlProvider>())
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
    }
}