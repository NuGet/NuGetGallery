// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Framework;
using NuGetGallery.Helpers;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class AdminApiControllerFacts
    {
        public class TheReflowMethod : TestContainer
        {
            private readonly Mock<IPackageService> _packageServiceMock;
            private readonly Mock<IEntitiesContext> _entitiesContextMock;
            private readonly Mock<IPackageFileService> _packageFileServiceMock;
            private readonly Mock<ITelemetryService> _telemetryServiceMock;

            private readonly Package _availablePackage;
            private readonly Package _availablePackage2;
            private readonly Package _deletedPackage;

            public TheReflowMethod()
            {
                var entitiesContextMock = ReflowServiceSetupHelper.SetupEntitiesContext();
                var database = new Mock<IDatabase>();
                database.Setup(x => x.BeginTransaction()).Returns(() => new Mock<IDbContextTransaction>().Object);
                entitiesContextMock.Setup(m => m.GetDatabase()).Returns(database.Object);

                _packageServiceMock = new Mock<IPackageService>();
                _entitiesContextMock = entitiesContextMock;
                _packageFileServiceMock = new Mock<IPackageFileService>();
                _telemetryServiceMock = new Mock<ITelemetryService>();

                _availablePackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Available.Package" },
                    Version = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                _availablePackage2 = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Available.Package.2" },
                    Version = "3.0.0-beta.1",
                    PackageStatusKey = PackageStatus.Available
                };

                _deletedPackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Deleted.Package" },
                    Version = "1.0.0",
                    PackageStatusKey = PackageStatus.Deleted
                };

                SetupPackages(_packageServiceMock, [_availablePackage, _availablePackage2, _deletedPackage]);
            }

            [Fact]
            public async Task Returns400WhenBodyIsInvalidJsonAsync()
            {
                var controller = CreateController(requestBody: "{invalid json");

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsNullAsync()
            {
                var request = new AdminReflowPackageRequest { Packages = null };
                var controller = CreateController(request: request);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminReflowPackageRequest { Packages = [] };
                var controller = CreateController(request: request);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesExceeds100Async()
            {
                var packages = new List<AdminReflowPackageIdentity>();
                for (int i = 0; i < 101; i++)
                {
                    packages.Add(new AdminReflowPackageIdentity { Id = $"Pkg{i}", Version = "1.0.0" });
                }

                var request = new AdminReflowPackageRequest { Packages = packages };
                var controller = CreateController(request: request);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenAllPackagesNotFoundAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = "DoesNotExist", Version = "1.0.0" }
                    ],
                    Reason = "test"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[0].Status);
            }

            [Fact]
            public async Task Returns202WithAcceptedPackagesAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "test reflow"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock,
                    telemetryService: _telemetryServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);

                _telemetryServiceMock.Verify(
                    t => t.TrackAdminApiReflow(1, 1, "test reflow", It.IsAny<string>()), Times.Once);
            }

            [Fact]
            public async Task Returns202WithMixedStatusesAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version },
                        new AdminReflowPackageIdentity { Id = "DoesNotExist", Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = _availablePackage2.Id, Version = _availablePackage2.Version }
                    ],
                    Reason = "mixed test"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[1].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[2].Status);
            }

            [Fact]
            public async Task ReturnsInvalidForBadVersionStringAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = "InvalidVersionPackage", Version = "not-a-version" },
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "version test"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[1].Status);
            }

            [Fact]
            public async Task DeduplicatesPackageIdentitiesAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version},
                        new AdminReflowPackageIdentity {Id = _availablePackage.Id, Version = _availablePackage.Version},
                        new AdminReflowPackageIdentity { Id = _availablePackage2.Id, Version = _availablePackage2.Version }
                    ],
                    Reason = "dedupe test"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock,
                    telemetryService: _telemetryServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[1].Status);

                _telemetryServiceMock.Verify(
                    t => t.TrackAdminApiReflow(3, 2, "dedupe test", It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsInvalidForNullIdOrVersionAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = null, Version = "1.0.0" },
                        new AdminReflowPackageIdentity { Id = "My.Package", Version = null },
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "null test"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.Invalid, response.Results[1].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[2].Status);
            }

            [Fact]
            public async Task ReturnsNotFoundForDeletedPackageAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = _deletedPackage.Id, Version = _deletedPackage.Version }
                    ],
                    Reason = "deleted test"
                };

                var controller = CreateController(
                    request: request,
                    packageService: _packageServiceMock,
                    entitiesContext: _entitiesContextMock,
                    packageFileService: _packageFileServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[0].Status);
            }

            private static void SetupPackages(Mock<IPackageService> packageServiceMock, List<Package> packages)
            {
                foreach (var package in packages)
                {
                    packageServiceMock
                        .Setup(s => s.FindPackageByIdAndVersionStrict(package.Id, package.Version))
                        .Returns(package);
                }
            }

            private static AdminApiController CreateController(
                AdminReflowPackageRequest request = null,
                string requestBody = null,
                Mock<IPackageService> packageService = null,
                Mock<IEntitiesContext> entitiesContext = null,
                Mock<IPackageFileService> packageFileService = null,
                Mock<ITelemetryService> telemetryService = null)
            {
                packageService ??= new Mock<IPackageService>();
                entitiesContext ??= new Mock<IEntitiesContext>();
                packageFileService ??= new Mock<IPackageFileService>();
                telemetryService ??= new Mock<ITelemetryService>();

                var controller = new AdminApiController(
                    packageService.Object,
                    entitiesContext.Object,
                    packageFileService.Object,
                    telemetryService.Object);

                var body = requestBody ?? (request != null ? JsonConvert.SerializeObject(request) : "{}");
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
