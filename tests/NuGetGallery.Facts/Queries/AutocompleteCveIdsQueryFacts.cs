// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Queries
{
    public class AutocompleteCveIdsQueryFacts
    {
        public class TheExecuteMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void ThrowsExceptionForNullOrEmptyArgument(string queryString)
            {
                var query = new AutocompleteCveIdsQuery(new FakeEntitiesContext());

                Assert.Throws<ArgumentNullException>(() => query.Execute(queryString));
            }


            [Theory]
            [InlineData("CVE-2000-01", "CVE-2000-01")]
            [InlineData("cVe-2000-01", "CVE-2000-01")]
            [InlineData("2000-01", "CVE-2000-01")]
            public void ReturnsExpectedResults(string queryString, string expectedCveIdStartString)
            {
                var entitiesContext = new FakeEntitiesContext();
                var expectedResult1 = new Cve { CveId = "CVE-2000-011", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cve { CveId = "CVE-2000-012", Description = "Description A: unlisted.", Listed = false };
                var expectedResult2 = new Cve { CveId = "CVE-2000-013", Description = "description B", Listed = true };
                var expectedResult3 = new Cve { CveId = "CVE-2000-014", Description = "description C", Listed = true };
                var expectedResult4 = new Cve { CveId = "CVE-2000-015", Description = "description D", Listed = true };
                var expectedResult5 = new Cve { CveId = "CVE-2000-016", Description = "description E", Listed = true };
                var notExpectedResult2 = new Cve { CveId = "CVE-2000-017", Description = "description F", Listed = true };
                entitiesContext.Cves.Add(expectedResult1);
                entitiesContext.Cves.Add(notExpectedResult1);
                entitiesContext.Cves.Add(expectedResult2);
                entitiesContext.Cves.Add(expectedResult3);
                entitiesContext.Cves.Add(expectedResult4);
                entitiesContext.Cves.Add(expectedResult5);
                entitiesContext.Cves.Add(notExpectedResult2);

                var query = new AutocompleteCveIdsQuery(entitiesContext);
                var queryResults = query.Execute(queryString);

                Assert.NotNull(queryResults);
                Assert.Equal(5, queryResults.Count);

                Assert.All(
                    queryResults,
                    r =>
                    {
                        Assert.StartsWith(expectedCveIdStartString, r.CveId, StringComparison.OrdinalIgnoreCase);

                        // Only the listed elements with CWE-ID starting with the query string should be returned (up to 5 elements).
                        Assert.NotEqual(notExpectedResult1.CveId, r.CveId, StringComparer.OrdinalIgnoreCase);

                        // Sorted numerically, this is the 6th element in the resultset and should be filtered out (max 5).
                        Assert.NotEqual(notExpectedResult2.CveId, r.CveId, StringComparer.OrdinalIgnoreCase);
                    });
            }
        }
    }
}
