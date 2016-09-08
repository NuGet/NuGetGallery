// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using NuGetGallery.Configuration;
using NuGetGallery.Controllers;
using System.Threading.Tasks;

namespace NuGetGallery.TestUtils.Infrastructure
{
    public class TestableV1Feed : ODataV1FeedController
    {
        public TestableV1Feed(
            IEntityRepository<Package> repo,
            IGalleryConfigurationService configuration,
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