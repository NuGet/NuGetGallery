// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Services
{
    public class AccessConditionWrapperFacts
    {
        [Theory]
        [MemberData(nameof(ETags))]
        public void GenerateIfMatchCondition(string etag)
        {
            var actual = AccessConditionWrapper.GenerateIfMatchCondition(etag);

            Assert.Equal(etag, actual.IfMatchETag);
            Assert.Null(actual.IfNoneMatchETag);
        }

        [Theory]
        [MemberData(nameof(ETags))]
        public void GenerateIfNoneMatchCondition(string etag)
        {
            var actual = AccessConditionWrapper.GenerateIfNoneMatchCondition(etag);

            Assert.Null(actual.IfMatchETag);
            Assert.Equal(etag, actual.IfNoneMatchETag);
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

        public static IEnumerable<object[]> ETags => new[]
        {
            new object[] { "*" },
            new object[] { "\"some-etag\"" },
            new object[] { "no quotes" },
            new object[] { "" },
            new object[] { null },
        };
    }
}
