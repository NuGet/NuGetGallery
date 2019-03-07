﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Moq;
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
            public void ReturnsExpectedResultsForNullOrEmptyArgument(string queryString)
            {
                var query = new AutocompleteCveIdsQuery(Mock.Of<IEntityRepository<Cve>>());
                var queryResults = query.Execute(queryString);

                Assert.False(queryResults.Success);
                Assert.Null(queryResults.Results);
                Assert.Equal(Strings.AutocompleteCveIds_ValidationError, queryResults.ErrorMessage);
            }

            [Theory]
            [InlineData("CVE-2000-01", "CVE-2000-01")]
            [InlineData("cVe-2000-01", "CVE-2000-01")]
            [InlineData("2000-01", "CVE-2000-01")]
            [InlineData("2000", "CVE-2000")]
            public void ReturnsExpectedResults(string queryString, string expectedCveIdStartString)
            {
                var expectedResult1 = new Cve { CveId = "CVE-2000-011", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cve { CveId = "CVE-2000-012", Description = "Description A: unlisted.", Listed = false };
                var expectedResult2 = new Cve { CveId = "CVE-2000-013", Description = "description B", Listed = true };
                var expectedResult3 = new Cve { CveId = "CVE-2000-014", Description = "description C", Listed = true };
                var expectedResult4 = new Cve { CveId = "CVE-2000-015", Description = "description D", Listed = true };
                var expectedResult5 = new Cve { CveId = "CVE-2000-016", Description = "description E", Listed = true };
                var notExpectedResult2 = new Cve { CveId = "CVE-2000-017", Description = "description F", Listed = true };

                var cveRepositoryMock = new Mock<IEntityRepository<Cve>>();
                cveRepositoryMock
                    .Setup(x => x.GetAll())
                    .Returns(new[] 
                    {
                        expectedResult1,
                        notExpectedResult1,
                        expectedResult2,
                        expectedResult3,
                        expectedResult4,
                        expectedResult5,
                        notExpectedResult2
                    }.AsQueryable());

                var query = new AutocompleteCveIdsQuery(cveRepositoryMock.Object);
                var queryResults = query.Execute(queryString);

                Assert.Equal(5, queryResults.Results.Count);
                Assert.True(queryResults.Success);
                Assert.Null(queryResults.ErrorMessage);
                Assert.All(
                    queryResults.Results,
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