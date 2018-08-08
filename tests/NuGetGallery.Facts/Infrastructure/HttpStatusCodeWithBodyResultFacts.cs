// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using Xunit;

namespace NuGetGallery
{
    public class HttpStatusCodeWithBodyResultFacts
    {
        [Theory]
        [InlineData("foo", "foo")]
        [InlineData("foo\rbar", "foo bar")]
        [InlineData("foo\nbar", "foo bar")]
        [InlineData("foo\r\nbar", "foo bar")]
        [InlineData(" foo\n\r  bar   \nbaz  ", "foo bar baz")]
        public void CollapsesLinesInStatusDescription(string input, string expected)
        {
            var result = new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, input);

            Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal(expected, result.StatusDescription);
            Assert.Equal(input, result.Body);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("foo")]
        [InlineData("  foo  ")]
        public void LeavesOneLinersAsIs(string input)
        {
            var result = new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, input);

            Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Same(input, result.StatusDescription);
            Assert.Same(input, result.Body);
        }
    }
}
