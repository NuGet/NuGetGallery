// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Queries
{
    public class CweIdHelperFacts
    {
        public class TheGetCweIdNumericPartAsIntegerMethod
        {
            [Theory]
            [InlineData("CWE-001", 1)]
            [InlineData("CWE-010", 10)]
            [InlineData("CWE-10", 10)]
            [InlineData(null, null)]
            public void ReturnsExpectedResult(string cweId, int? expectedResult)
            {
                var actualResult = CweIdHelper.GetCweIdNumericPartAsInteger(cweId);

                Assert.Equal(expectedResult, actualResult);
            }
        }

        public class TheStartsWithCweIdPrefixMethod
        {
            [Theory]
            [InlineData("CWE-001", true)]
            [InlineData("Cwe-001", true)]
            [InlineData("cwe-001", true)]
            [InlineData("cve-001", false)]
            [InlineData("001", false)]
            [InlineData(" ", false)]
            [InlineData("", false)]
            [InlineData(null, false)]
            public void ReturnsExpectedResult(string cweId, bool expectedResult)
            {
                var actualResult = CweIdHelper.StartsWithCweIdPrefix(cweId);

                Assert.Equal(expectedResult, actualResult);
            }
        }
    }
}