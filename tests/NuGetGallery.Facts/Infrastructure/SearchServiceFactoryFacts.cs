// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Xunit;

namespace NuGetGallery
{
    public class SearchServiceFactoryFacts
    {
        public SearchServiceFactoryFacts()
        {
            SearchService = new Mock<ISearchService>();
            PreviewSearchService = new Mock<ISearchService>();

            Target = new SearchServiceFactory(
                SearchService.Object,
                PreviewSearchService.Object);
        }

        public Mock<ISearchService> SearchService { get; }
        public Mock<ISearchService> PreviewSearchService { get; }
        public SearchServiceFactory Target { get; }

        [Fact]
        public void GetServiceReturnsNonPreview()
        {
            var actual = Target.GetService();

            Assert.Same(SearchService.Object, actual);
        }

        [Fact]
        public void GetPreviewServiceReturnsPreview()
        {
            var actual = Target.GetPreviewService();

            Assert.Same(PreviewSearchService.Object, actual);
        }
    }
}
