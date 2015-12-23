// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using NuGetGallery.Configuration;
using NuGetGallery.Controllers;

namespace NuGetGallery.TestUtils.Infrastructure
{
    public class TestableV2Feed : ODataV2FeedController
    {
        public TestableV2Feed(
            IEntityRepository<Package> repo,
            ConfigurationService configuration,
            ISearchService searchService)
            : base(repo, configuration, searchService)
        {
        }

        protected override HttpContextBase GetTraditionalHttpContext()
        {
            return FeedServiceHelpers.GetMockContext();
        }

        public string GetSiteRootForTest()
        {
            return GetSiteRoot();
        }
    }
}