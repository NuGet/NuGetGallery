// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class RewriteBaseUrlMessageInspectorFacts
    {
        public class TheRewriteUrlPathMethod
        {
            [Theory]
            [InlineData("/api/v2/curated-feed", "/api/v2/curated-feeds/foo", "foo")]
            [InlineData("/api/v2/curated-feed/Packages", "/api/v2/curated-feeds/foo/Packages", "foo")]
            [InlineData("/api/v2/curated-feed/Packages/jQuery/abc/123", "/api/v2/curated-feeds/foo/Packages/jQuery/abc/123", "foo")]
            [InlineData("/api/v2/curated-feed/", "/api/v2/curated-feeds/foo/", "foo")]
            [InlineData("/api/v2/curated-feed/Packages/", "/api/v2/curated-feeds/foo/Packages/", "foo")]
            [InlineData("/api/v2/curated-feed/Packages/jQuery/abc/123/", "/api/v2/curated-feeds/foo/Packages/jQuery/abc/123/", "foo")]
            public void CorrectlyRewritesURLsToCuratedFeeds(string start, string rewritten, string curatedFeedName)
            {
                Assert.Equal(rewritten, RewriteBaseUrlMessageInspector.RewriteUrlPath(start, curatedFeedName));
            }
        }
    }
}
