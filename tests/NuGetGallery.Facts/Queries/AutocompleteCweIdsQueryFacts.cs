// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Queries
{
    public class AutocompleteCweIdsQueryFacts
    {
        public class TheExecuteMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void ThrowsExceptionForNullOrEmptyArgument(string queryString)
            {
                var query = new AutocompleteCweIdsQuery(new FakeEntitiesContext());

                Assert.Throws<ArgumentNullException>(() => query.Execute(queryString));
            }

            [Theory]
            [InlineData("0")]
            [InlineData("cWe-0")]
            public void WhenQueryingByCweIdReturnsNullIfQueryStringTooShort(string queryString)
            {
                var query = new AutocompleteCweIdsQuery(new FakeEntitiesContext());
                var result = query.Execute(queryString);

                Assert.Null(result);
            }

            [Fact]
            public void WhenQueryingByNameReturnsNullIfQueryStringTooShort()
            {
                var query = new AutocompleteCweIdsQuery(new FakeEntitiesContext());
                var result = query.Execute("abc");

                Assert.Null(result);
            }

            [Theory]
            [InlineData("CWE-01", "CWE-01")]
            [InlineData("cWe-01", "CWE-01")]
            [InlineData("01", "CWE-01")]
            public void WhenQueryingByIdReturnsExpectedResults(string queryString, string expectedCweIdStartString)
            {
                var entitiesContext = new FakeEntitiesContext();
                var expectedResult1 = new Cwe { CweId = "CWE-011", Name = "Name A: listed", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cwe { CweId = "CWE-012", Name = "Name A: unlisted", Description = "Description A: unlisted.", Listed = false };
                var expectedResult2 = new Cwe { CweId = "CWE-013", Name = "Name B", Description = "description B", Listed = true };
                var expectedResult3 = new Cwe { CweId = "CWE-014", Name = "Name C", Description = "description C", Listed = true };
                var expectedResult4 = new Cwe { CweId = "CWE-015", Name = "Name D", Description = "description D", Listed = true };
                var expectedResult5 = new Cwe { CweId = "CWE-016", Name = "Name E", Description = "description E", Listed = true };
                var notExpectedResult2 = new Cwe { CweId = "CWE-017", Name = "Name F", Description = "description F", Listed = true };
                entitiesContext.Cwes.Add(expectedResult1);
                entitiesContext.Cwes.Add(notExpectedResult1);
                entitiesContext.Cwes.Add(expectedResult2);
                entitiesContext.Cwes.Add(expectedResult3);
                entitiesContext.Cwes.Add(expectedResult4);
                entitiesContext.Cwes.Add(expectedResult5);
                entitiesContext.Cwes.Add(notExpectedResult2);

                var query = new AutocompleteCweIdsQuery(entitiesContext);
                var queryResults = query.Execute(queryString);

                Assert.NotNull(queryResults);
                Assert.Equal(5, queryResults.Count);

                Assert.All(
                    queryResults,
                    r =>
                    {
                        Assert.StartsWith(expectedCweIdStartString, r.CweId, StringComparison.OrdinalIgnoreCase);

                        // Only the listed elements with CWE-ID starting with the query string should be returned (up to 5 elements).
                        Assert.NotEqual(notExpectedResult1.CweId, r.CweId, StringComparer.OrdinalIgnoreCase);

                        // Sorted numerically, this is the 6th element in the resultset and should be filtered out (max 5).
                        Assert.NotEqual(notExpectedResult2.CweId, r.CweId, StringComparer.OrdinalIgnoreCase);
                    });
            }

            [Fact]
            public void WhenQueryingByNameReturnsExpectedResults()
            {
                var entitiesContext = new FakeEntitiesContext();
                var expectedResult = new Cwe { CweId = "CWE-001", Name = "Name A: listed", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cwe { CweId = "CWE-002", Name = "Name A: unlisted", Description = "Description A: unlisted.", Listed = false };
                var notExpectedResult2 = new Cwe { CweId = "CWE-003", Name = "Name B", Description = "description B", Listed = true };
                entitiesContext.Cwes.Add(expectedResult);
                entitiesContext.Cwes.Add(notExpectedResult1);
                entitiesContext.Cwes.Add(notExpectedResult2);

                var query = new AutocompleteCweIdsQuery(entitiesContext);
                var queryResults = query.Execute("Name A");

                Assert.NotNull(queryResults);
                Assert.Single(queryResults);

                Assert.Collection(
                    queryResults,
                    r =>
                    {
                        // Only the listed element matching by name should be returned.
                        Assert.Equal(expectedResult.Name, r.Name);
                        Assert.Equal(expectedResult.CweId, r.CweId);
                        Assert.Equal(expectedResult.Description, r.Description);
                    });
            }
        }
    }
}