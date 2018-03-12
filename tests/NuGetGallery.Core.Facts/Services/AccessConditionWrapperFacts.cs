// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Services
{
    public class AccessConditionWrapperFacts
    {
        [Theory]
        [InlineData("*")]
        [InlineData("\"some-etag\"")]
        [InlineData("no quotes")]
        [InlineData("")]
        [InlineData(null)]
        public void GenerateIfMatchCondition(string etag)
        {
            var actual = AccessConditionWrapper.GenerateIfMatchCondition(etag);

            Assert.Equal(etag, actual.IfMatchETag);
            Assert.Null(actual.IfNoneMatchETag);
        }

        [Fact]
        public void GenerateIfNotExistsCondition()
        {
            var actual = AccessConditionWrapper.GenerateIfNotExistsCondition();

            Assert.Null(actual.IfMatchETag);
            Assert.Equal("*", actual.IfNoneMatchETag);
        }

        [Fact]
        public void GenerateEmptyCondition()
        {
            var actual = AccessConditionWrapper.GenerateEmptyCondition();

            Assert.Null(actual.IfMatchETag);
            Assert.Null(actual.IfNoneMatchETag);
        }
    }
}
