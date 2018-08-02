// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class NumberExtensionsFacts
    {
        public class TheToUserFriendlyBytesLabelMethod
        {
            [Fact]
            public void ThrowsArgumentOutOfRangeExceptionForNegativeParameterValue()
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => NumberExtensions.ToUserFriendlyBytesLabel(-1));
            }

            [Theory]
            [InlineData(1, "1 byte")]
            [InlineData(512, "512 bytes")]
            [InlineData(1024, "1 KB")]
            [InlineData(1024*1024, "1 MB")]
            [InlineData(1024 * 1024 * 1024, "1 GB")]
            public void FormatsUsingExpectedUnit(long bytes, string expected)
            {
                var actual = NumberExtensions.ToUserFriendlyBytesLabel(bytes);

                Assert.Equal(expected, actual);
            }
        }
    }
}
