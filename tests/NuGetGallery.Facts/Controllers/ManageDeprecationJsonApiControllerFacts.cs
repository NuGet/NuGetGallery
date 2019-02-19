// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ManageDeprecationJsonApiControllerFacts
    {
        public class TheGetCweIdsMethod : TestContainer
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("0")]
            [InlineData("cWe-0")]
            [InlineData("abc")]
            public void ReturnsHttp400ForValidationErrors(string queryString)
            {
                var result = InvokeAndAssertStatusCode(queryString, HttpStatusCode.BadRequest);

                Assert.False(result.Success);
                Assert.Equal(Strings.AutocompleteCweIds_ValidationError, result.ErrorMessage);
            }

            [Theory]
            [InlineData("CWE-01", "CWE-01")]
            [InlineData("cWe-01", "CWE-01")]
            [InlineData("01", "CWE-01")]
            public void ReturnsHttp200AndExpectedBodyForValidRequests(string queryString, string expectedCweIdStartString)
            {
                var entitiesContext = Get<IEntitiesContext>();

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

                var queryResults = InvokeAndAssertStatusCode(queryString, HttpStatusCode.OK);

                Assert.True(queryResults.Success);
                Assert.Null(queryResults.ErrorMessage);

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

            private AutocompleteCweIdQueryResults InvokeAndAssertStatusCode(string queryString, HttpStatusCode expectedStatusCode)
            {
                var fakes = Get<Fakes>();
                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(fakes.User);

                var result = controller.GetCweIds(queryString);
                Assert.Equal((int)expectedStatusCode, controller.Response.StatusCode);

                return result.Data as AutocompleteCweIdQueryResults;
            }
        }

        public class TheGetCveIdsMethod : TestContainer
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("0")]
            [InlineData("cVe-0")]
            [InlineData("abc")]
            public void ReturnsHttp400ForValidationErrors(string queryString)
            {
                var result = InvokeAndAssertStatusCode(queryString, HttpStatusCode.BadRequest);

                Assert.False(result.Success);
                Assert.Equal(Strings.AutocompleteCveIds_ValidationError, result.ErrorMessage);
            }

            [Theory]
            [InlineData("CVE-2000-01", "CVE-2000-01")]
            [InlineData("cVe-2000-01", "CVE-2000-01")]
            [InlineData("2000-01", "CVE-2000-01")]
            [InlineData("2000", "CVE-2000")]
            public void ReturnsHttp200AndExpectedBodyForValidRequests(string queryString, string expectedCveIdStartString)
            {
                var entitiesContext = Get<IEntitiesContext>();

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

                var queryResults = InvokeAndAssertStatusCode(queryString, HttpStatusCode.OK);

                Assert.True(queryResults.Success);
                Assert.Null(queryResults.ErrorMessage);

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

            private AutocompleteCveIdQueryResults InvokeAndAssertStatusCode(string queryString, HttpStatusCode expectedStatusCode)
            {
                var fakes = Get<Fakes>();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(fakes.User);

                var result = controller.GetCveIds(queryString);
                Assert.Equal((int)expectedStatusCode, controller.Response.StatusCode);

                return result.Data as AutocompleteCveIdQueryResults;
            }
        }
    }
}