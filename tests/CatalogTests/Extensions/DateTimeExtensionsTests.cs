// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace CatalogTests.Extensions
{
    public class DateTimeExtensionsTests
    {
        public class TheForceUtcMethod
        {
            [Fact]
            public void ConvertsLocalTimeToUtc()
            {
                var localTime = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Local);

                var convertedTime = localTime.ForceUtc();

                Assert.Equal(DateTimeKind.Utc, convertedTime.Kind);
            }

            [Fact]
            public void ConvertsUnspecifiedTimeToUtc()
            {
                var unspecifiedTime = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

                var convertedTime = unspecifiedTime.ForceUtc();

                Assert.Equal(DateTimeKind.Utc, convertedTime.Kind);
            }
        }
    }
}