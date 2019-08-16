// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
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
            public void ReturnsProperJsonResult()
            {
                // Arrange
                var id = "Crested.Gecko";

                var versions = new[] { "1.0.0", "2.0.0" };
                GetMock<IPackageDeprecationManagementService>()
                    .Setup(x => x.GetPossibleAlternatePackageVersions(id))
                    .Returns(versions);

                var controller = GetController<ManageDeprecationJsonApiController>();

                // Act
                var result = controller.GetAlternatePackageVersions(id);

                // Assert
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, controller.Response.StatusCode);
                Assert.Equal(versions, result.Data);
            }
        }

        public class TheDeprecateMethod : TestContainer
        {
            public static IEnumerable<object[]> ReturnsProperJsonResult_Data =
                MemberDataHelper.Combine(
                    Enumerable
                        .Repeat(
                            MemberDataHelper.BooleanDataSet(), 4)
                        .ToArray());

            [Theory]
            [MemberData(nameof(ReturnsProperJsonResult_Data))]
            public async Task ReturnsProperJsonResult(
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                bool success)
            {
                // Arrange
                var id = "Crested.Gecko";
                var versions = new[] { "1.0.0", "2.0.0" };
                var alternateId = "alt.Id";
                var alternateVersion = "3.0.0";
                var customMessage = "custom";

                var currentUser = Get<Fakes>().User;

                var errorStatus = HttpStatusCode.InternalServerError;
                var errorMessage = "woops";
                var deprecationService = GetMock<IPackageDeprecationManagementService>();
                deprecationService
                    .Setup(x => x.UpdateDeprecation(
                        currentUser,
                        id,
                        versions,
                        isLegacy,
                        hasCriticalBugs,
                        isOther,
                        alternateId,
                        alternateVersion,
                        customMessage))
                    .ReturnsAsync(success ? null : new UpdateDeprecationError(errorStatus, errorMessage))
                    .Verifiable();

                var controller = GetController<ManageDeprecationJsonApiController>();
                controller.SetCurrentUser(currentUser);

                var request = new DeprecatePackageRequest
                {
                    Id = id,
                    Versions = versions,
                    IsLegacy = isLegacy,
                    HasCriticalBugs = hasCriticalBugs,
                    IsOther = isOther,
                    AlternatePackageId = alternateId,
                    AlternatePackageVersion = alternateVersion,
                    CustomMessage = customMessage
                };

                // Act
                var result = await controller.Deprecate(request);

                // Assert
                if (success)
                {
                    AssertSuccessResponse(controller);
                }
                else
                {
                    AssertErrorResponse(controller, result, errorStatus, errorMessage);
                }

                deprecationService.Verify();
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