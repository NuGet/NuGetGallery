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
            [InlineData(1,                  "1")]
            [InlineData(999,              "999")]
            [InlineData(1000,            "1.0K")]
            [InlineData(1450,            "1.5K")]
            [InlineData(1900,            "1.9K")]
            [InlineData(1990,            "2.0K")]
            [InlineData(1999,            "2.0K")]
            [InlineData(9940,            "9.9K")]
            [InlineData(9949,            "9.9K")]
            [InlineData(9950,           "10.0K")]
            [InlineData(9999,           "10.0K")]
            [InlineData(10_000,         "10.0K")]
            [InlineData(99_949,         "99.9K")]
            [InlineData(99_950,        "100.0K")]
            [InlineData(99_990,        "100.0K")]
            [InlineData(100_000,       "100.0K")]
            [InlineData(100_990,       "101.0K")]
            [InlineData(100_999,       "101.0K")]
            [InlineData(999_000,       "999.0K")]
            [InlineData(999_999,         "1.0M")]
            [InlineData(1_000_000,       "1.0M")]
            [InlineData(9_949_000,       "9.9M")]
            [InlineData(9_950_000,      "10.0M")]
            [InlineData(9_999_000,      "10.0M")]
            [InlineData(99_990_000,    "100.0M")]
            [InlineData(100_940_000,   "100.9M")]
            [InlineData(100_949_000,   "100.9M")]
            [InlineData(100_950_000,   "101.0M")]
            [InlineData(100_990_000,   "101.0M")]
            [InlineData(999_990_000,     "1.0B")]
            [InlineData(1_000_000_000,   "1.0B")]
            [InlineData(1_999_000_000,   "2.0B")]
            [InlineData(-1,                 "-1")]
            [InlineData(-999,             "-999")]
            [InlineData(-1000,           "-1.0K")]
            [InlineData(-1450,           "-1.5K")]
            [InlineData(-1900,           "-1.9K")]
            [InlineData(-1990,           "-2.0K")]
            [InlineData(-1999,           "-2.0K")]
            [InlineData(-9940,           "-9.9K")]
            [InlineData(-9949,           "-9.9K")]
            [InlineData(-9950,          "-10.0K")]
            [InlineData(-9999,          "-10.0K")]
            [InlineData(-10_000,        "-10.0K")]
            [InlineData(-99_949,        "-99.9K")]
            [InlineData(-99_950,       "-100.0K")]
            [InlineData(-99_990,       "-100.0K")]
            [InlineData(-100_000,      "-100.0K")]
            [InlineData(-100_990,      "-101.0K")]
            [InlineData(-100_999,      "-101.0K")]
            [InlineData(-999_000,      "-999.0K")]
            [InlineData(-999_999,        "-1.0M")]
            [InlineData(-1_000_000,      "-1.0M")]
            [InlineData(-9_949_000,      "-9.9M")]
            [InlineData(-9_950_000,     "-10.0M")]
            [InlineData(-9_999_000,     "-10.0M")]
            [InlineData(-99_990_000,   "-100.0M")]
            [InlineData(-100_940_000,  "-100.9M")]
            [InlineData(-100_949_000,  "-100.9M")]
            [InlineData(-100_950_000,  "-101.0M")]
            [InlineData(-100_990_000,  "-101.0M")]
            [InlineData(-999_990_000,    "-1.0B")]
            [InlineData(-1_000_000_000,  "-1.0B")]
            [InlineData(-1_999_000_000,  "-2.0B")]
            public void FormatsUsingExpectedUnit(int number, string expected)
            {
                var actual = NumberExtensions.ToKiloFormat(number);

                Assert.Equal(expected, actual);
            }
        }
    }
}
