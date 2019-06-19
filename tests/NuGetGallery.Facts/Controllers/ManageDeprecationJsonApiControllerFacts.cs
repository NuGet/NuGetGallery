// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using NuGetGallery.RequestModels;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ManageDeprecationJsonApiControllerFacts
    {
        public class TheGetAlternatePackageVersionsMethod : TestContainer
        {
            [Fact]
            public void ReturnsEmptyIfNoVersions()
            {
                // Arrange
                var id = "Crested.Gecko";
                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.None))
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
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.None))
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
            public async Task ReturnsBadRequestIfOtherAndNoCustomMessage()
            {
                // Arrange
                var controller = GetController<ManageDeprecationJsonApiController>();

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest("id", isOther: true));

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.BadRequest, Strings.DeprecatePackage_CustomMessageRequired);
            }

            public static IEnumerable<object[]> ReturnsBadRequestIfNoVersions_Data =
                MemberDataHelper.AsDataSet(null, new string[0]);

            [Theory]
            [MemberData(nameof(ReturnsBadRequestIfNoVersions_Data))]
            public async Task ReturnsBadRequestIfNoVersions(IEnumerable<string> versions)
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest("id", versions));

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.BadRequest, Strings.DeprecatePackage_NoVersions);
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
            [MemberData(nameof(ReturnsNotFoundIfNoPackagesOrRegistrationMissing_Data))]
            public async Task ReturnsNotFoundIfNoPackagesOrRegistrationMissing(IEnumerable<Package> packages)
            {
                // Arrange
                var id = "id";
                var currentUser = TestUtility.FakeUser;

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(packages.ToList())
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest("id", new[] { "1.0.0" }));

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.NotFound, string.Format(Strings.DeprecatePackage_MissingRegistration, id));
                packageService.Verify();
            }


            [Fact]
            public async Task ReturnsForbiddenIfFeatureFlagDisabled()
            {
                // Arrange
                var id = "id";
                var currentUser = TestUtility.FakeUser;

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(false)
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(id, new[] { "1.0.0" }));

                // Assert
                AssertErrorResponse(controller, result, HttpStatusCode.Forbidden, Strings.DeprecatePackage_Forbidden);
                featureFlagService.Verify();
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

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(id, new[] { "1.0.0" }));

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

                var registration = new PackageRegistration
                {
                    Id = id,
                    IsLocked = true
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(id, new[] { "1.0.0" }));

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
            public async Task ReturnsBadRequestIfLongCustomMessage(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

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
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                var customMessage = new string('a', 4001);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(
                        id,
                        new[] { "1.0.0" },
                        customMessage: customMessage));

                // Assert
                AssertErrorResponse(
                    controller,
                    result,
                    HttpStatusCode.BadRequest,
                    string.Format(Strings.DeprecatePackage_CustomMessageTooLong, 4000));
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfAlternatePackageRegistrationMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
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
                    CreateDeprecatePackageRequest(
                        id, 
                        new[] { "1.0.0" }, 
                        alternatePackageId: alternatePackageId));

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

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
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
                    CreateDeprecatePackageRequest(
                        id, 
                        new[] { "1.0.0" }, 
                        alternatePackageId: alternatePackageId, 
                        alternatePackageVersion: alternatePackageVersion));

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

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(id, new[] { "1.0.0" }));

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

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(id, new[] { "1.0.0" }));

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
                        PackageDeprecationStatus.CriticalBugs);
                    yield return MemberDataHelper.AsData(true, false, false,
                        PackageDeprecationStatus.Legacy);
                    yield return MemberDataHelper.AsData(true, false, true,
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(true, true, false,
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.CriticalBugs);
                    yield return MemberDataHelper.AsData(false, true, true,
                        PackageDeprecationStatus.CriticalBugs | PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(true, true, true,
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.CriticalBugs | PackageDeprecationStatus.Other);
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
                    MemberDataHelper.EnumDataSet<ReturnsSuccessful_AlternatePackage_State>()).ToList();

            [Theory]
            [MemberData(nameof(ReturnsSuccessful_Data))]
            public Task ReturnsSuccessfulWithCustomMessage(
                User currentUser,
                User owner,
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                PackageDeprecationStatus expectedStatus,
                ReturnsSuccessful_AlternatePackage_State alternatePackageState)
            {
                return AssertSuccessful(
                    currentUser,
                    owner,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    expectedStatus,
                    alternatePackageState,
                    true);
            }

            /// <remarks>
            /// Deprecations where the only reason is "other" must have a custom message.
            /// </remarks>
            public static IEnumerable<object[]> ReturnsSuccessfulWithoutCustomMessage_Data =
                ReturnsSuccessful_Data.Where(x => !(bool)x[4]);

            [Theory]
            [MemberData(nameof(ReturnsSuccessfulWithoutCustomMessage_Data))]
            public Task ReturnsSuccessfulWithoutCustomMessage(
                User currentUser,
                User owner,
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                PackageDeprecationStatus expectedStatus,
                ReturnsSuccessful_AlternatePackage_State alternatePackageState)
            {
                return AssertSuccessful(
                    currentUser,
                    owner,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    expectedStatus,
                    alternatePackageState,
                    false);
            }

            private async Task AssertSuccessful(
                User currentUser,
                User owner,
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                PackageDeprecationStatus expectedStatus,
                ReturnsSuccessful_AlternatePackage_State alternatePackageState,
                bool hasCustomMessage)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

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
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
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
                }
                else if (alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Package)
                {
                    packageService
                        .Setup(x => x.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion))
                        .Returns(alternatePackage)
                        .Verifiable();
                }

                var deprecationService = GetMock<IPackageDeprecationService>();

                var customMessage = hasCustomMessage ? "<message>" : null;
                var encodedCustomMessage = hasCustomMessage ? "&lt;message&gt;" : null;

                deprecationService
                    .Setup(x => x.UpdateDeprecation(
                        new[] { package, package2 },
                        expectedStatus,
                        alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Registration ? alternatePackageRegistration : null,
                        alternatePackageState == ReturnsSuccessful_AlternatePackage_State.Package ? alternatePackage : null,
                        encodedCustomMessage,
                        currentUser))
                    .Completes()
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                var packageNormalizedVersions = new[] { package.NormalizedVersion, package2.NormalizedVersion };

                // Act
                var result = await controller.Deprecate(
                    CreateDeprecatePackageRequest(
                        id,
                        packageNormalizedVersions,
                        isLegacy,
                        hasCriticalBugs,
                        isOther,
                        alternatePackageId,
                        alternatePackageVersion,
                        customMessage));

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

            private static DeprecatePackageRequest CreateDeprecatePackageRequest(
                string id = null,
                IEnumerable<string> versions = null,
                bool isLegacy = false,
                bool hasCriticalBugs = false,
                bool isOther = false,
                string alternatePackageId = null,
                string alternatePackageVersion = null,
                string customMessage = null)
            {
                return new DeprecatePackageRequest
                {
                    Id = id,
                    Versions = versions,
                    IsLegacy = isLegacy,
                    HasCriticalBugs = hasCriticalBugs,
                    IsOther = isOther,
                    AlternatePackageId = alternatePackageId,
                    AlternatePackageVersion = alternatePackageVersion,
                    CustomMessage = customMessage
                };
            }
        }
    }
}