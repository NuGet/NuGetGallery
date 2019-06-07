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

        public class TheToKiloFormatMethod
        {
            [Theory]
            [InlineData(1, "1")]
            [InlineData(999, "999")]
            [InlineData(1000, "1.00K")]
            [InlineData(1990, "1.99K")]
            [InlineData(1999, "1.99K")]
            [InlineData(9990, "9.99K")]
            [InlineData(9999, "9.99K")]
            [InlineData(10_000, "10.0K")]
            [InlineData(10_990, "10.9K")]
            [InlineData(99_990, "99.9K")]
            [InlineData(100_000, "100K")]
            [InlineData(100_990, "100K")]
            [InlineData(100_999, "100K")]
            [InlineData(999_000, "999K")]
            [InlineData(999_999, "999K")]
            [InlineData(1_000_000, "1.00M")]
            
            public void FormatsUsingExpectedUnit(int number, string expected)
            {
                var actual = NumberExtensions.ToKiloFormat(number);

                Assert.Equal(expected, actual);
            }
        }
    }
}
