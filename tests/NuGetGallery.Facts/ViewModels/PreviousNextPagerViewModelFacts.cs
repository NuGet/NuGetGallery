// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class PreviousNextPagerViewModelFacts
    {
        [Fact]
        public void PagerAtFirstPageDoesNotHavePreviousPageButDoesHaveNext()
        {
            // Arrange
            const int currentPageIndex = 0;

            // Act
            var pager = new PreviousNextPagerViewModel<int>(new[] { 0, 1, 2 }, currentPageIndex, 2, p => p.ToString());

            // Assert
            Assert.False(pager.HasPreviousPage);
            Assert.True(pager.HasNextPage);
            Assert.Equal("2", pager.NextPageUrl);
        }

        [Fact]
        public void PagerWithPageSizeGreaterThanItemsDoesNotHavePreviousOrNext()
        {
            // Arrange
            const int currentPageIndex = 0;

            // Act
            var pager = new PreviousNextPagerViewModel<int>(new[] { 0, 1, 2 }, currentPageIndex, 1, p => p.ToString());

            // Assert
            Assert.False(pager.HasPreviousPage);
            Assert.False(pager.HasNextPage);
        }

        [Fact]
        public void PagerWithPageSizeEqualToItemsCountDoesNotHavePreviousOrNext()
        {
            // Arrange
            const int currentPageIndex = 0;

            // Act
            var pager = new PreviousNextPagerViewModel<int>(new[] { 0, 1, 2, 3, 4 }, currentPageIndex, 1, p => p.ToString());

            // Assert
            Assert.False(pager.HasPreviousPage);
            Assert.False(pager.HasNextPage);
        }

        [Fact]
        public void LastPageShowsPreviousLinkButNotNextLink()
        {
            // Arrange
            const int currentPageIndex = 1;

            // Act
            var pager = new PreviousNextPagerViewModel<int>(new[] { 0, 1, 2, 3, 4, 5 }, currentPageIndex, 2, p => p.ToString());

            // Assert
            Assert.Equal("1", pager.PreviousPageUrl);
            Assert.True(pager.HasPreviousPage);
            Assert.False(pager.HasNextPage);
        }
    }
}