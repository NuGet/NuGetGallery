// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Queries
{
    public class CveIdHelperFacts
    {
        public class TheStartsWithCveIdPrefixMethod
        {
            [Theory]
            [InlineData("CVE-001", true)]
            [InlineData("Cve-001", true)]
            [InlineData("cve-001", true)]
            [InlineData("cwe-001", false)]
            [InlineData("001", false)]
            [InlineData(" ", false)]
            [InlineData("", false)]
            [InlineData(null, false)]
            public void ReturnsExpectedResult(string cveId, bool expectedResult)
            {
                var actualResult = CveIdHelper.StartsWithCveIdPrefix(cveId);

                Assert.Equal(expectedResult, actualResult);
            }
        }
    }
}