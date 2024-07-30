// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class PathUtilityFacts
    {
        [Fact]
        public void IsFilePathAbsoluteThrowsWhenPathIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => PathUtility.IsFilePathAbsolute(null));
        }

        [Theory]
        [InlineData(@"", false)]
        [InlineData(@"c:", false)]
        [InlineData(@"aaa.txt", false)]
        [InlineData(@"bbb\aaa.txt", false)]
        [InlineData(@"c:aaa.txt", false)]
        [InlineData(@"c:bbb\aaa.txt", false)]
        [InlineData(@"c:\", true)]
        [InlineData(@"c:\aaa.txt", true)]
        [InlineData(@"c:\bbb\aaa.txt", true)]
        public void IsFilePathAbsoluteSmokeTest(string path, bool expectedResult)
        {
            var result = PathUtility.IsFilePathAbsolute(path);
            Assert.Equal(expectedResult, result);
        }
    }
}
