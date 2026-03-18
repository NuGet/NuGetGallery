// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class AdminApiControllerFacts
    {
        public class TheReflowMethod
        {
            [Fact]
            public void Returns400WhenBodyIsInvalidJson()
            {
                var controller = CreateController(requestBody: "{invalid json");

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public void Returns400WhenPackagesIsNull()
            {
                var request = new AdminReflowPackageRequest { Packages = null };
                var controller = CreateController(request: request);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public void Returns400WhenPackagesIsEmpty()
            {
                var request = new AdminReflowPackageRequest { Packages = new List<AdminReflowPackageIdentity>() };
                var controller = CreateController(request: request);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public void Returns400WhenPackagesExceeds100()
            {
                var packages = new List<AdminReflowPackageIdentity>();
                for(int i = 0; i < 101; i++)
                {
                    packages.Add(new AdminReflowPackageIdentity { Id = $"Pkg{i}", Version = "1.0.0" });
                }

                var request = new AdminReflowPackageRequest { Packages = packages };
                var controller = CreateController(request: request);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public void Returns400WhenAllPackagesNotFound()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = "Does.Not.Exist", Version = "1.0.0" }
                    },
                    Reason = "test"
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Does.Not.Exist", "1.0.0"))
                    .Returns((Package)null);

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[0].Status);
            }

            [Fact]
            public void Returns202WithAcceptedPackages()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = "1.0.0" }
                    },
                    Reason = "test reflow"
                };

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "My.Package" },
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("My.Package", "1.0.0"))
                    .Returns(package);

                var mockTelemetry = new Mock<ITelemetryService>();

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService,
                    telemetryService: mockTelemetry);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);

                mockTelemetry.Verify(
                    t => t.TrackAdminApiReflow(1, 1, "test reflow", It.IsAny<string>()), Times.Once);
            }

            [Fact]
            public void Returns202WithMixedStatuses()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = "Good.Package", Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = "Bad Id!", Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = "Missing.Package", Version = "2.0.0" },
                        new AdminReflowPackageIdentity { Id = "Also.Good", Version = "3.0.0-beta.1" }
                    },
                    Reason = "mixed test"
                };

                var goodPackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Good.Package" },
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                var alsoGoodPackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Also.Good" },
                    NormalizedVersion = "3.0.0-beta.1",
                    PackageStatusKey = PackageStatus.Available
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Good.Package", "1.0.0"))
                    .Returns(goodPackage);
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Bad Id!", "1.0.0"))
                    .Returns((Package)null);
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Missing.Package", "2.0.0"))
                    .Returns((Package)null);
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Also.Good", "3.0.0-beta.1"))
                    .Returns(alsoGoodPackage);

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(4, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[1].Status);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[2].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[3].Status);
            }

            [Fact]
            public void ReturnsInvalidForBadVersionString()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = "not-a-version" },
                        new AdminReflowPackageIdentity { Id = "Good.Package", Version = "1.0.0" }
                    },
                    Reason = "version test"
                };

                var goodPackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Good.Package" },
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Good.Package", "1.0.0"))
                    .Returns(goodPackage);

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[1].Status);
            }

            [Fact]
            public void DeduplicatesPackageIdentities()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = "1.0.0.0" }
                    },
                    Reason = "dedupe test"
                };

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "My.Package" },
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("My.Package", "1.0.0"))
                    .Returns(package);

                var mockTelemetry = new Mock<ITelemetryService>();

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService,
                    telemetryService: mockTelemetry);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);

                mockTelemetry.Verify(
                    t => t.TrackAdminApiReflow(3, 1, "dedupe test", It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public void ReturnsInvalidForNullIdOrVersion()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = null, Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = null },
                        new AdminReflowPackageIdentity { Id = "Good.Package", Version = "1.0.0" }
                    },
                    Reason = "null test"
                };

                var goodPackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Good.Package" },
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Good.Package", "1.0.0"))
                    .Returns(goodPackage);

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.Invalid, response.Results[1].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[2].Status);
            }

            [Fact]
            public void ReturnsNotFoundForDeletedPackage()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages = new List<AdminReflowPackageIdentity>
                    {
                        new AdminReflowPackageIdentity { Id = "Deleted.Package", Version = "1.0.0" }
                    },
                    Reason = "deleted test"
                };

                var deletedPackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Deleted.Package" },
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Deleted
                };

                var mockPackageService = new Mock<IPackageService>();
                mockPackageService
                    .Setup(s => s.FindPackageByIdAndVersionStrict("Deleted.Package", "1.0.0"))
                    .Returns(deletedPackage);

                var controller = CreateController(
                    request: request,
                    packageService: mockPackageService);

                var result = controller.ReflowPackage() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[0].Status);
            }

            private static AdminApiController CreateController(
                AdminReflowPackageRequest request = null,
                string requestBody = null,
                Mock<IPackageService> packageService = null,
                Mock<IEntitiesContext> entitiesContext = null,
                Mock<IPackageFileService> packageFileService = null,
                Mock<ITelemetryService> telemetryService = null)
            {
                packageService = packageService ?? new Mock<IPackageService>();
                entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
                packageFileService = packageFileService ?? new Mock<IPackageFileService>();
                telemetryService = telemetryService ?? new Mock<ITelemetryService>();

                var controller = new AdminApiController(
                    packageService.Object,
                    telemetryService.Object);

                var body = requestBody ??(request != null
                    ? JsonConvert.SerializeObject(request)
                    : "{}");
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var inputStream = new MemoryStream(bodyBytes);

                var mockRequest = new Mock<HttpRequestBase>();
                mockRequest.Setup(r => r.InputStream).Returns(inputStream);

                var mockResponse = new Mock<HttpResponseBase>();
                mockResponse.SetupProperty(r => r.StatusCode);
                mockResponse.SetupProperty(r => r.TrySkipIisCustomErrors);

                var mockHttpContext = new Mock<HttpContextBase>();
                mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
                mockHttpContext.Setup(c => c.Response).Returns(mockResponse.Object);
                mockHttpContext.SetupGet(c => c.Items).Returns(new Dictionary<object, object>
                {
                    { "owin.Environment", new Dictionary<string, object>() }
                });

                var controllerContext = new ControllerContext
                {
                    HttpContext = mockHttpContext.Object,
                    RouteData = new RouteData(),
                    Controller = controller
                };

                controller.ControllerContext = controllerContext;

                return controller;
            }

            private static T GetResponseData<T>(JsonResult result)
            {
                var json = JsonConvert.SerializeObject(result.Data);
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
    }
}
