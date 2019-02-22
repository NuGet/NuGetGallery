// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
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
                var cweRepositoryMock = GetMock<IEntityRepository<Cwe>>();

                var expectedResult1 = new Cwe { CweId = "CWE-011", Name = "Name A: listed", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cwe { CweId = "CWE-012", Name = "Name A: unlisted", Description = "Description A: unlisted.", Listed = false };
                var expectedResult2 = new Cwe { CweId = "CWE-013", Name = "Name B", Description = "description B", Listed = true };
                var expectedResult3 = new Cwe { CweId = "CWE-014", Name = "Name C", Description = "description C", Listed = true };
                var expectedResult4 = new Cwe { CweId = "CWE-015", Name = "Name D", Description = "description D", Listed = true };
                var expectedResult5 = new Cwe { CweId = "CWE-016", Name = "Name E", Description = "description E", Listed = true };
                var notExpectedResult2 = new Cwe { CweId = "CWE-017", Name = "Name F", Description = "description F", Listed = true };

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
                var cveRepositoryMock = GetMock<IEntityRepository<Cve>>();

                var expectedResult1 = new Cve { CveId = "CVE-2000-011", Description = "Description A: listed.", Listed = true };
                var notExpectedResult1 = new Cve { CveId = "CVE-2000-012", Description = "Description A: unlisted.", Listed = false };
                var expectedResult2 = new Cve { CveId = "CVE-2000-013", Description = "description B", Listed = true };
                var expectedResult3 = new Cve { CveId = "CVE-2000-014", Description = "description C", Listed = true };
                var expectedResult4 = new Cve { CveId = "CVE-2000-015", Description = "description D", Listed = true };
                var expectedResult5 = new Cve { CveId = "CVE-2000-016", Description = "description E", Listed = true };
                var notExpectedResult2 = new Cve { CveId = "CVE-2000-017", Description = "description F", Listed = true };

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

        public class TheGetAlternatePackageVersionsMethod : TestContainer
        {
            [Fact]
            public void ReturnsNotFoundIfIdMissing()
            {
                // Arrange
                var id = "missingId";
                GetMock<IPackageService>()
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns((PackageRegistration)null);

                var controller = GetController<ManageDeprecationJsonApiController>();

                // Act
                var result = controller.GetAlternatePackageVersions(id);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
                Assert.Null(result.Data);
            }

            [Fact]
            public void ReturnsNotFoundIfNoVersions()
            {
                // Arrange
                var id = "Crested.Gecko";
                var registration = new PackageRegistration
                {
                    Id = id
                };

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns(registration);

                var controller = GetController<ManageDeprecationJsonApiController>();

                // Act
                var result = controller.GetAlternatePackageVersions(id);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
                Assert.Null(result.Data);
            }

            [Fact]
            public void ReturnsAllAvailableVersionsInReverseVersionOrder()
            {
                // Arrange
                var id = "Crested.Gecko";
                var firstPackage = new Package
                {
                    Version = "1.0.0+build"
                };

                var secondPackage = new Package
                {
                    Version = "2.0.0+build"
                };

                var thirdPackage = new Package
                {
                    Version = "3.0.0+build"
                };

                var deletedPackage = new Package
                {
                    Version = "2.1.0",
                    PackageStatusKey = PackageStatus.Deleted
                };

                var validatingPackage = new Package
                {
                    Version = "2.1.0",
                    PackageStatusKey = PackageStatus.Validating
                };

                var failedValidationPackage = new Package
                {
                    Version = "2.1.0",
                    PackageStatusKey = PackageStatus.FailedValidation
                };

                var registration = new PackageRegistration
                {
                    Id = id,
                    Packages = new[] 
                    {
                        firstPackage,
                        deletedPackage,
                        validatingPackage,
                        failedValidationPackage,
                        thirdPackage,
                        secondPackage
                    }
                };

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns(registration);

                var controller = GetController<ManageDeprecationJsonApiController>();

                // Act
                var result = controller.GetAlternatePackageVersions(id);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, controller.Response.StatusCode);

                var expected = new[] { thirdPackage, secondPackage, firstPackage }
                    .Select(v => v.Version)
                    .ToList();

                Assert.Equal(expected, result.Data);
            }
        }
    }
}