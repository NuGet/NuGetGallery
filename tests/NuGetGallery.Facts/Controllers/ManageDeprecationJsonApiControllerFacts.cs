// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
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
            public void ReturnsEmptyIfNoVersions()
            {
                // Arrange
                var id = "Crested.Gecko";
                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesById(id, false))
                    .Returns(new Package[0]);

                var controller = GetController<ManageDeprecationJsonApiController>();

                // Act
                var result = controller.GetAlternatePackageVersions(id);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, controller.Response.StatusCode);
                Assert.Empty((IEnumerable<string>)result.Data);
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

                var packages = new[]
                {
                    firstPackage,
                    deletedPackage,
                    validatingPackage,
                    failedValidationPackage,
                    thirdPackage,
                    secondPackage
                };

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesById(id, false))
                    .Returns(packages);

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

        public class TheDeprecateMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsForbiddenIfFeatureFlagDisabled()
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(false)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    "id", null, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
                featureFlagService.Verify();
            }

            public static IEnumerable<object[]> ReturnsBadRequestIfNoVersions_Data = 
                MemberDataHelper.AsDataSet(null, new string[0]);

            [Theory]
            [MemberData(nameof(ReturnsBadRequestIfNoVersions_Data))]
            public async Task ReturnsBadRequestIfNoVersions(IEnumerable<string> versions)
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    "id", versions, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.BadRequest, Strings.DeprecatePackage_NoVersions);
                featureFlagService.Verify();
            }

            public static IEnumerable<object[]> ReturnsNotFoundIfNoPackagesOrRegistrationMissing_Data
            {
                get
                {
                    var packageWithNullRegistration = new Package();
                    return MemberDataHelper.AsDataSet(
                        new Package[0], 
                        new[] { packageWithNullRegistration });
                }
            }

            [Theory]
            [InlineData("yabba-dabba-doo")] // Doesn't match at all
            [InlineData("CVE-2019")] // Missing number
            [InlineData("CVE-2019-234")] // Number not long enough
            [InlineData("CVE-1998-43244")] // Year too old
            [InlineData("CVE-9999-1323")] // Year in the future...if NuGet.org has lasted 7980 years since the creation of this unit test, congratulations!
            public async Task ReturnsBadRequestIfCveIdInvalid(string invalidId)
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    "id", new[] { "1.0.0" }, false, false, false, new[] { "CVE-2019-1111", invalidId }, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.BadRequest,
                    string.Format(Strings.DeprecatePackage_InvalidCve, invalidId));
                featureFlagService.Verify();
            }

            [Fact]
            public async Task ReturnsBadRequestIfCweIdInvalid()
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                var invalidId = "yabba-dabba-doo";

                // Act
                var result = await controller.Deprecate(
                    "id", new[] { "1.0.0" }, false, false, false, null, null, new[] { "CWE-1", invalidId }, null, null, null, false);

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.BadRequest,
                    string.Format(Strings.DeprecatePackage_InvalidCwe, invalidId));
                featureFlagService.Verify();
            }

            [Theory]
            [InlineData(-1)]
            [InlineData(11)]
            public async Task ReturnsBadRequestIfCvssInvalid(decimal cvss)
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    "id", new[] { "1.0.0" }, false, false, false, null, cvss, null, null, null, null, false);

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.BadRequest, Strings.DeprecatePackage_InvalidCvss);
                featureFlagService.Verify();
            }

            [Theory]
            [MemberData(nameof(ReturnsNotFoundIfNoPackagesOrRegistrationMissing_Data))]
            public async Task ReturnsNotFoundIfNoPackagesOrRegistrationMissing(IEnumerable<Package> packages)
            {
                // Arrange
                var id = "id";
                var currentUser = TestUtility.FakeUser;

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(packages.ToList())
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { "1.0.0" }, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.NotFound, string.Format(Strings.DeprecatePackage_MissingRegistration, id));
                featureFlagService.Verify();
                packageService.Verify();
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task ReturnsForbiddenIfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { "1.0.0" }, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
                featureFlagService.Verify();
                packageService.Verify();
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsForbiddenIfLocked(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id,
                    IsLocked = true
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { "1.0.0" }, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(
                    controller, 
                    result, 
                    HttpStatusCode.Forbidden, 
                    string.Format(Strings.DeprecatePackage_Locked, id));
                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfAlternatePackageRegistrationMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package })
                    .Verifiable();

                var alternatePackageId = "altId";
                packageService
                    .Setup(x => x.FindPackageRegistrationById(alternatePackageId))
                    .Returns((PackageRegistration)null)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { "1.0.0" }, false, false, false, null, null, null, alternatePackageId, null, null, false);

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_NoAlternatePackageRegistration, alternatePackageId));
                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfAlternatePackageVersionMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package })
                    .Verifiable();

                var alternatePackageId = "altId";
                var alternatePackageVersion = "1.2.3-alt";
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion))
                    .Returns((Package)null)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { "1.0.0" }, false, false, false, null, null, null, alternatePackageId, alternatePackageVersion, null, false);

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_NoAlternatePackage, alternatePackageId, alternatePackageVersion));
                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfVersionMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { "1.0.0" }, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_MissingVersion, id));
                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfSomeVersionMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, new[] { package.NormalizedVersion, "1.0.0" }, false, false, false, null, null, null, null, null, null, false);

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.NotFound,
                    string.Format(Strings.DeprecatePackage_MissingVersion, id));
                featureFlagService.Verify();
                packageService.Verify();
            }

            private static void AssertErrorResponse(
                ManageDeprecationJsonApiController controller,
                JsonResult result,
                HttpStatusCode code,
                string error)
            {
                AssertResponseStatusCode(controller, code);

                // Using JObject to get the property from the result easily.
                // Alternatively we could use reflection, but this is easier, and makes sense as the response is intended to be JSON anyway.
                var jObject = JObject.FromObject(result.Data);
                Assert.Equal(error, jObject["error"].Value<string>());
            }

            public static IEnumerable<object[]> PackageDeprecationStates_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(false, false, false, 
                        PackageDeprecationStatus.NotDeprecated);
                    yield return MemberDataHelper.AsData(false, false, true, 
                        PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(false, true, false, 
                        PackageDeprecationStatus.Legacy);
                    yield return MemberDataHelper.AsData(false, true, true, 
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(true, false, false, 
                        PackageDeprecationStatus.Vulnerable);
                    yield return MemberDataHelper.AsData(true, false, true, 
                        PackageDeprecationStatus.Vulnerable | PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(true, true, false, 
                        PackageDeprecationStatus.Vulnerable | PackageDeprecationStatus.Legacy);
                    yield return MemberDataHelper.AsData(true, true, true, 
                        PackageDeprecationStatus.Vulnerable | PackageDeprecationStatus.Legacy | PackageDeprecationStatus.Other);
                }
            }

            public enum ReturnsSuccessful_AlternatePackage_State
            {
                None,
                Registration,
                Package
            }

            public static IEnumerable<object[]> ReturnsSuccessful_Data =
                MemberDataHelper.Combine(
                    Owner_Data,
                    PackageDeprecationStates_Data,
                    MemberDataHelper.EnumDataSet<ReturnsSuccessful_AlternatePackage_State>(),
                    MemberDataHelper.BooleanDataSet(),
                    MemberDataHelper.BooleanDataSet()).ToList();

            [Theory]
            [MemberData(nameof(ReturnsSuccessful_Data))]
            public async Task ReturnsSuccessful(
                User currentUser, 
                User owner, 
                bool isVulnerable, 
                bool isLegacy, 
                bool isOther, 
                PackageDeprecationStatus expectedStatus, 
                ReturnsSuccessful_AlternatePackage_State alternatePackageState, 
                bool hasAdditionalData,
                bool shouldUnlist)
            {
                // Arrange
                var id = "id";

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser))
                    .Returns(true)
                    .Verifiable();

                var registration = new PackageRegistration
                {
                    Id = id
                };

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var package2 = new Package
                {
                    NormalizedVersion = "1.0.0",
                    PackageRegistration = registration
                };

                var unselectedPackage = new Package
                {
                    NormalizedVersion = "1.3.2",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, true))
                    .Returns(new[] { package, package2, unselectedPackage })
                    .Verifiable();

                var alternatePackageId = alternatePackageState != ReturnsSuccessful_AlternatePackage_State.None ? "altId" : null;
                var alternatePackageVersion = alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Package ? "1.2.3-alt" : null;

                var alternatePackageRegistration = new PackageRegistration
                {
                    Id = alternatePackageId
                };

                var alternatePackage = new Package
                {
                    NormalizedVersion = alternatePackageVersion,
                    PackageRegistration = alternatePackageRegistration
                };

                if (alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Registration)
                {
                    packageService
                        .Setup(x => x.FindPackageRegistrationById(alternatePackageId))
                        .Returns(alternatePackageRegistration)
                        .Verifiable();
                } else if (alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Package)
                {
                    packageService
                        .Setup(x => x.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion))
                        .Returns(alternatePackage)
                        .Verifiable();
                }

                var deprecationService = GetMock<IPackageDeprecationService>();

                var cveIds = hasAdditionalData ? new[] { "CVE-2019-1111", "CVE-2019-22222", "CVE-2019-333333" } : null;
                var cves = cveIds?.Select(i => new Cve { CveId = i }).ToArray() ?? new Cve[0];
                deprecationService
                    .Setup(x => x.GetOrCreateCvesByIdAsync(cveIds ?? Enumerable.Empty<string>(), false))
                    .CompletesWith(cves)
                    .Verifiable();

                var cvss = hasAdditionalData ? (decimal?)5.5 : null;

                var cweIds = hasAdditionalData ? new[] { "CWE-1", "CWE-2", "CWE-3" } : null;
                var cwes = cweIds?.Select(i => new Cwe { CweId = i }).ToArray() ?? new Cwe[0];
                deprecationService
                    .Setup(x => x.GetOrCreateCwesByIdAsync(cweIds ?? Enumerable.Empty<string>(), false))
                    .CompletesWith(cwes)
                    .Verifiable();

                var customMessage = hasAdditionalData ? "message" : null;

                deprecationService
                    .Setup(x => x.UpdateDeprecation(
                        new[] { package, package2 },
                        expectedStatus,
                        cves,
                        cvss,
                        cwes,
                        alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Registration ? alternatePackageRegistration : null,
                        alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Package ? alternatePackage : null,
                        customMessage,
                        shouldUnlist))
                    .Completes()
                    .Verifiable();
                    
                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    id, 
                    new[] { package.NormalizedVersion, package2.NormalizedVersion }, 
                    isVulnerable, 
                    isLegacy, 
                    isOther,
                    cveIds, 
                    cvss,
                    cweIds,
                    alternatePackageId, 
                    alternatePackageVersion, 
                    customMessage, 
                    shouldUnlist);

                // Assert
                AssertSuccessResponse(controller);
                featureFlagService.Verify();
                packageService.Verify();
                deprecationService.Verify();
            }

            private static void AssertSuccessResponse(
                ManageDeprecationJsonApiController controller)
            {
                AssertResponseStatusCode(controller, HttpStatusCode.OK);
            }

            private static void AssertResponseStatusCode(
                ManageDeprecationJsonApiController controller,
                HttpStatusCode code)
            {
                Assert.Equal((int)code, controller.Response.StatusCode);
            }
        }
    }
}