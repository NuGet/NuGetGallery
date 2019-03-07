// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Moq;
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
            [InlineData("0")]
            [InlineData("cWe-0")]
            [InlineData("abc")]
            public void ReturnsExpectedResultsIfQueryStringTooShort(string queryString)
            {
                var query = new AutocompleteCweIdsQuery(Mock.Of<IEntityRepository<Cwe>>());
                var queryResults = query.Execute(queryString);

                Assert.False(queryResults.Success);
                Assert.Equal(Strings.AutocompleteCweIds_ValidationError, queryResults.ErrorMessage);
                Assert.Null(queryResults.Results);
            }

            [Theory]
            [InlineData("CWE-01", "CWE-01")]
            [InlineData("cWe-01", "CWE-01")]
            [InlineData("01", "CWE-01")]
            public void WhenQueryingByIdReturnsExpectedResults(string queryString, string expectedCweIdStartString)
            {
                var expectedResult1 = new Cwe { CweId = "CWE-011", Name = "Name A: listed", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cwe { CweId = "CWE-012", Name = "Name A: unlisted", Description = "Description A: unlisted.", Listed = false };
                var expectedResult2 = new Cwe { CweId = "CWE-013", Name = "Name B", Description = "description B", Listed = true };
                var expectedResult3 = new Cwe { CweId = "CWE-014", Name = "Name C", Description = "description C", Listed = true };
                var expectedResult4 = new Cwe { CweId = "CWE-015", Name = "Name D", Description = "description D", Listed = true };
                var expectedResult5 = new Cwe { CweId = "CWE-016", Name = "Name E", Description = "description E", Listed = true };
                var notExpectedResult2 = new Cwe { CweId = "CWE-017", Name = "Name F", Description = "description F", Listed = true };

                var cweRepositoryMock = new Mock<IEntityRepository<Cwe>>();
                cweRepositoryMock
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

                var query = new AutocompleteCweIdsQuery(cweRepositoryMock.Object);
                var queryResults = query.Execute(queryString);

                Assert.Equal(5, queryResults.Results.Count);
                Assert.True(queryResults.Success);
                Assert.Null(queryResults.ErrorMessage);

                Assert.All(
                    queryResults.Results,
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
                var expectedResult = new Cwe { CweId = "CWE-001", Name = "Name A: listed", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cwe { CweId = "CWE-002", Name = "Name A: unlisted", Description = "Description A: unlisted.", Listed = false };
                var notExpectedResult2 = new Cwe { CweId = "CWE-003", Name = "Name B", Description = "description B", Listed = true };

                var cweRepositoryMock = new Mock<IEntityRepository<Cwe>>();
                cweRepositoryMock
                    .Setup(x => x.GetAll())
                    .Returns(new[]
                    {
                        expectedResult,
                        notExpectedResult1,
                        notExpectedResult2
                    }.AsQueryable());

                var query = new AutocompleteCweIdsQuery(cweRepositoryMock.Object);
                var queryResults = query.Execute("Name A");

                Assert.NotNull(queryResults);
                var singleResult = Assert.Single(queryResults.Results);

                // Only the listed element matching by name should be returned.
                Assert.Equal(expectedResult.Name, singleResult.Name);
                Assert.Equal(expectedResult.CweId, singleResult.CweId);
                Assert.Equal(expectedResult.Description, singleResult.Description);
            }
        }
    }
}