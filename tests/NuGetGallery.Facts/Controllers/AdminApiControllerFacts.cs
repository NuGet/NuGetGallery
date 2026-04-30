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
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class AdminApiControllerFacts
    {
        private static AdminApiController CreateController(
            string requestBody = null,
            string callerAzp = null,
            Mock<IPackageService> packageService = null,
            Mock<IReflowPackageService> reflowPackageService = null,
            Mock<ILockPackageService> lockPackageService = null,
            Mock<ILockUserService> lockUserService = null,
            Mock<IPackageDeleteService> packageDeleteService = null)
        {
            packageService ??= new Mock<IPackageService>();
            reflowPackageService ??= new Mock<IReflowPackageService>();
            lockPackageService ??= new Mock<ILockPackageService>();
            lockUserService ??= new Mock<ILockUserService>();
            packageDeleteService ??= new Mock<IPackageDeleteService>();

            var controller = new AdminApiController(
                packageService.Object,
                reflowPackageService.Object,
                lockPackageService.Object,
                lockUserService.Object,
                packageDeleteService.Object);

            var body = requestBody ?? "{}";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var inputStream = new MemoryStream(bodyBytes);

            var mockRequest = new Mock<HttpRequestBase>();
            mockRequest.Setup(r => r.InputStream).Returns(inputStream);

            var mockResponse = new Mock<HttpResponseBase>();
            mockResponse.SetupProperty(r => r.StatusCode);
            mockResponse.SetupProperty(r => r.TrySkipIisCustomErrors);

            var items = new Dictionary<object, object>
            {
                { "owin.Environment", new Dictionary<string, object>() }
            };

            if (callerAzp != null)
            {
                items[AdminApiAuthAttribute.AzpItemKey] = callerAzp;
            }

            var mockHttpContext = new Mock<HttpContextBase>();
            mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
            mockHttpContext.Setup(c => c.Response).Returns(mockResponse.Object);
            mockHttpContext.SetupGet(c => c.Items).Returns(items);

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

        private static void SetupPackages(Mock<IPackageService> packageServiceMock, List<Package> packages)
        {
            foreach (var package in packages)
            {
                packageServiceMock
                    .Setup(s => s.FindPackageByIdAndVersionStrict(package.Id, package.Version))
                    .Returns(package);
            }
        }

        public class TheReflowMethod : TestContainer
        {
            private readonly Mock<IPackageService> _packageServiceMock;
            private readonly Mock<IReflowPackageService> _reflowPackageServiceMock;

            private readonly Package _availablePackage;
            private readonly Package _availablePackage2;
            private readonly Package _deletedPackage;

            public TheReflowMethod()
            {
                _packageServiceMock = new Mock<IPackageService>();
                _reflowPackageServiceMock = new Mock<IReflowPackageService>();

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
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminReflowPackageRequest { Packages = [] };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

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
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

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
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

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
                    requestBody: JsonConvert.SerializeObject(request),
                    callerAzp: "test-app",
                    packageService: _packageServiceMock,
                    reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);

                _reflowPackageServiceMock.Verify(
                    s => s.ReflowAsync(_availablePackage.Id, _availablePackage.Version, "test reflow", "test-app"),
                    Times.Once);
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
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

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
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

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
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version },
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version },
                        new AdminReflowPackageIdentity { Id = _availablePackage2.Id, Version = _availablePackage2.Version }
                    ],
                    Reason = "dedupe test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock,
                    reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[1].Status);

                _reflowPackageServiceMock.Verify(
                    s => s.ReflowAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(2));
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
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

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
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

                var result = await controller.ReflowPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[0].Status);
            }
        }

        public class TheLockPackageMethod : TestContainer
        {
            [Fact]
            public async Task Returns400WhenBodyIsInvalidJsonAsync()
            {
                var controller = CreateController(requestBody: "{invalid json");

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsNullAsync()
            {
                var request = new AdminLockPackageRequest { Packages = null };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminLockPackageRequest { Packages = [] };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesExceeds100Async()
            {
                var packages = new List<AdminLockPackageIdentity>();
                for (int i = 0; i < 101; i++)
                {
                    packages.Add(new AdminLockPackageIdentity { Id = $"Pkg{i}" });
                }

                var request = new AdminLockPackageRequest { Packages = packages };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenAllPackagesInvalidAsync()
            {
                var request = new AdminLockPackageRequest
                {
                    Packages =
                    [
                        new AdminLockPackageIdentity { Id = null },
                        new AdminLockPackageIdentity { Id = "" }
                    ]
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.All(response.Results, r => Assert.Equal(AdminLockPackageStatus.Invalid, r.Status));
            }

            [Fact]
            public async Task Returns202WithAcceptedPackagesAsync()
            {
                var lockPackageService = new Mock<ILockPackageService>();
                lockPackageService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockPackageServiceResult.Success);

                var request = new AdminLockPackageRequest
                {
                    Packages = [new AdminLockPackageIdentity { Id = "My.Package" }],
                    Reason = "test lock"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    callerAzp: "test-app",
                    lockPackageService: lockPackageService);

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminLockPackageStatus.Accepted, response.Results[0].Status);

                lockPackageService.Verify(
                    s => s.SetLockStateAsync("My.Package", true, "test lock", "test-app"),
                    Times.Once);
            }

            [Fact]
            public async Task DeduplicatesPackageIdsCaseInsensitiveAsync()
            {
                var lockPackageService = new Mock<ILockPackageService>();
                lockPackageService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockPackageServiceResult.Success);

                var request = new AdminLockPackageRequest
                {
                    Packages =
                    [
                        new AdminLockPackageIdentity { Id = "My.Package" },
                        new AdminLockPackageIdentity { Id = "my.package" },
                        new AdminLockPackageIdentity { Id = "Other.Package" }
                    ],
                    Reason = "dedupe test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    lockPackageService: lockPackageService);

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);

                lockPackageService.Verify(
                    s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task ReturnsInvalidForNullIdAsync()
            {
                var lockPackageService = new Mock<ILockPackageService>();
                lockPackageService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockPackageServiceResult.Success);

                var request = new AdminLockPackageRequest
                {
                    Packages =
                    [
                        new AdminLockPackageIdentity { Id = null },
                        new AdminLockPackageIdentity { Id = "Valid.Package" }
                    ],
                    Reason = "null test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    lockPackageService: lockPackageService);

                var result = await controller.LockPackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminLockPackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminLockPackageStatus.Accepted, response.Results[1].Status);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task PassesLockedFieldToServiceAsync(bool locked)
            {
                var lockPackageService = new Mock<ILockPackageService>();
                lockPackageService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockPackageServiceResult.Success);

                var request = new AdminLockPackageRequest
                {
                    Packages = [new AdminLockPackageIdentity { Id = "My.Package" }],
                    Locked = locked,
                    Reason = "toggle test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    lockPackageService: lockPackageService);

                await controller.LockPackageAsync();

                lockPackageService.Verify(
                    s => s.SetLockStateAsync("My.Package", locked, "toggle test", It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task PassesReasonAndCallerAzpAsync()
            {
                var lockPackageService = new Mock<ILockPackageService>();
                lockPackageService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockPackageServiceResult.Success);

                var request = new AdminLockPackageRequest
                {
                    Packages = [new AdminLockPackageIdentity { Id = "My.Package" }],
                    Reason = "security incident"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    callerAzp: "my-service-principal",
                    lockPackageService: lockPackageService);

                await controller.LockPackageAsync();

                lockPackageService.Verify(
                    s => s.SetLockStateAsync("My.Package", true, "security incident", "my-service-principal"),
                    Times.Once);
            }
        }

        public class TheLockUserMethod : TestContainer
        {
            [Fact]
            public async Task Returns400WhenBodyIsInvalidJsonAsync()
            {
                var controller = CreateController(requestBody: "{invalid json");

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenUsersIsNullAsync()
            {
                var request = new AdminLockUserRequest { Users = null };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenUsersIsEmptyAsync()
            {
                var request = new AdminLockUserRequest { Users = [] };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenUsersExceeds10Async()
            {
                var users = new List<AdminUserIdentity>();
                for (int i = 0; i < 11; i++)
                {
                    users.Add(new AdminUserIdentity { Username = $"user{i}" });
                }

                var request = new AdminLockUserRequest { Users = users };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenAllUsersInvalidAsync()
            {
                var request = new AdminLockUserRequest
                {
                    Users =
                    [
                        new AdminUserIdentity { Username = null },
                        new AdminUserIdentity { Username = "" }
                    ]
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockUserResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.All(response.Results, r => Assert.Equal(AdminLockUserStatus.Invalid, r.Status));
            }

            [Fact]
            public async Task Returns202WithAcceptedUsersAsync()
            {
                var lockUserService = new Mock<ILockUserService>();
                lockUserService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockUserServiceResult.Success);

                var request = new AdminLockUserRequest
                {
                    Users = [new AdminUserIdentity { Username = "badactor42" }],
                    Reason = "TOS violation"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    callerAzp: "test-app",
                    lockUserService: lockUserService);

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockUserResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminLockUserStatus.Accepted, response.Results[0].Status);

                lockUserService.Verify(
                    s => s.SetLockStateAsync("badactor42", true, "TOS violation", "test-app"),
                    Times.Once);
            }

            [Fact]
            public async Task DeduplicatesUsernamesCaseInsensitiveAsync()
            {
                var lockUserService = new Mock<ILockUserService>();
                lockUserService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockUserServiceResult.Success);

                var request = new AdminLockUserRequest
                {
                    Users =
                    [
                        new AdminUserIdentity { Username = "Alice" },
                        new AdminUserIdentity { Username = "alice" },
                        new AdminUserIdentity { Username = "Bob" }
                    ],
                    Reason = "dedupe test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    lockUserService: lockUserService);

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockUserResponse>(result);
                Assert.Equal(2, response.Results.Count);

                lockUserService.Verify(
                    s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task ReturnsInvalidForNullUsernameAsync()
            {
                var lockUserService = new Mock<ILockUserService>();
                lockUserService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockUserServiceResult.Success);

                var request = new AdminLockUserRequest
                {
                    Users =
                    [
                        new AdminUserIdentity { Username = null },
                        new AdminUserIdentity { Username = "validuser" }
                    ],
                    Reason = "null test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    lockUserService: lockUserService);

                var result = await controller.LockUserAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockUserResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminLockUserStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminLockUserStatus.Accepted, response.Results[1].Status);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task PassesLockedFieldToServiceAsync(bool locked)
            {
                var lockUserService = new Mock<ILockUserService>();
                lockUserService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockUserServiceResult.Success);

                var request = new AdminLockUserRequest
                {
                    Users = [new AdminUserIdentity { Username = "testuser" }],
                    Locked = locked,
                    Reason = "toggle test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    lockUserService: lockUserService);

                await controller.LockUserAsync();

                lockUserService.Verify(
                    s => s.SetLockStateAsync("testuser", locked, "toggle test", It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task PassesReasonAndCallerAzpAsync()
            {
                var lockUserService = new Mock<ILockUserService>();
                lockUserService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockUserServiceResult.Success);

                var request = new AdminLockUserRequest
                {
                    Users = [new AdminUserIdentity { Username = "testuser" }],
                    Reason = "TOS violation"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    callerAzp: "my-service-principal",
                    lockUserService: lockUserService);

                await controller.LockUserAsync();

                lockUserService.Verify(
                    s => s.SetLockStateAsync("testuser", true, "TOS violation", "my-service-principal"),
                    Times.Once);
            }
        }

        public class TheSoftDeletePackageMethod : TestContainer
        {
            private readonly Mock<IPackageService> _packageServiceMock;
            private readonly Mock<IPackageDeleteService> _packageDeleteServiceMock;

            private readonly Package _availablePackage;
            private readonly Package _availablePackage2;
            private readonly Package _deletedPackage;

            public TheSoftDeletePackageMethod()
            {
                _packageServiceMock = new Mock<IPackageService>();
                _packageDeleteServiceMock = new Mock<IPackageDeleteService>();

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

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsNullAsync()
            {
                var request = new AdminSoftDeletePackageRequest { Packages = null };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminSoftDeletePackageRequest { Packages = [] };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesExceeds100Async()
            {
                var packages = new List<AdminSoftDeletePackageIdentity>();
                for (int i = 0; i < 101; i++)
                {
                    packages.Add(new AdminSoftDeletePackageIdentity { Id = $"Pkg{i}", Version = "1.0.0" });
                }

                var request = new AdminSoftDeletePackageRequest { Packages = packages };
                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request));

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenAllPackagesNotFoundAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "DoesNotExist", Version = "1.0.0" }
                    ],
                    Reason = "test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[0].Status);
            }

            [Fact]
            public async Task Returns202WithAcceptedPackagesAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "malware"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    callerAzp: "test-app",
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[0].Status);

                _packageDeleteServiceMock.Verify(
                    s => s.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => new List<Package>(p).Count == 1),
                        null,
                        "malware",
                        "test-app"),
                    Times.Once);
            }

            [Fact]
            public async Task Returns202WithMixedStatusesAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version },
                        new AdminSoftDeletePackageIdentity { Id = "DoesNotExist", Version = "1.0.0" },
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage2.Id, Version = _availablePackage2.Version }
                    ],
                    Reason = "mixed test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[1].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[2].Status);
            }

            [Fact]
            public async Task ReturnsInvalidForBadVersionStringAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "BadVersion", Version = "not-a-version" },
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "version test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.Equal(AdminSoftDeletePackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[1].Status);
            }

            [Fact]
            public async Task DeduplicatesPackageIdentitiesAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version },
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version },
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage2.Id, Version = _availablePackage2.Version }
                    ],
                    Reason = "dedupe test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.All(response.Results, r => Assert.Equal(AdminSoftDeletePackageStatus.Accepted, r.Status));
            }

            [Fact]
            public async Task ReturnsInvalidForNullIdOrVersionAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = null, Version = "1.0.0" },
                        new AdminSoftDeletePackageIdentity { Id = "My.Package", Version = null },
                        new AdminSoftDeletePackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "null test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminSoftDeletePackageStatus.Invalid, response.Results[0].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.Invalid, response.Results[1].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[2].Status);
            }

            [Fact]
            public async Task ReturnsNotFoundForDeletedPackageAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = _deletedPackage.Id, Version = _deletedPackage.Version }
                    ],
                    Reason = "deleted test"
                };

                var controller = CreateController(
                    requestBody: JsonConvert.SerializeObject(request),
                    packageService: _packageServiceMock);

                var result = await controller.SoftDeletePackageAsync() as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[0].Status);
            }
        }
    }
}
