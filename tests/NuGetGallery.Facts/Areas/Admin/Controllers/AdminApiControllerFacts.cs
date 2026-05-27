// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Controllers;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class AdminApiControllerFacts
    {
        private static AdminApiController CreateController(
            string callerAzp = null,
            Mock<IPackageService> packageService = null,
            Mock<IReflowPackageService> reflowPackageService = null,
            Mock<ILockPackageService> lockPackageService = null,
            Mock<ILockUserService> lockUserService = null,
            Mock<IPackageDeleteService> packageDeleteService = null,
            Mock<IFeatureFlagService> featureFlagService = null)
        {
            packageService ??= new Mock<IPackageService>();
            reflowPackageService ??= new Mock<IReflowPackageService>();
            lockPackageService ??= new Mock<ILockPackageService>();
            lockUserService ??= new Mock<ILockUserService>();
            packageDeleteService ??= new Mock<IPackageDeleteService>();
            featureFlagService ??= new Mock<IFeatureFlagService>();

            var controller = new AdminApiController(
                packageService.Object,
                reflowPackageService.Object,
                lockPackageService.Object,
                lockUserService.Object,
                packageDeleteService.Object,
                featureFlagService.Object);

            var mockResponse = new Mock<HttpResponseBase>();
            mockResponse.SetupProperty(r => r.StatusCode);
            mockResponse.SetupProperty(r => r.TrySkipIisCustomErrors);

            var items = new Dictionary<object, object>
            {
                { "owin.Environment", new Dictionary<string, object>() }
            };

            if (callerAzp != null)
            {
                items[AdminApiAuthAttribute.CallerIdentityItemKey] = callerAzp;
            }

            var mockHttpContext = new Mock<HttpContextBase>();
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

        private static void ValidateModel(Controller controller, object model)
        {
            var validationContext = new ValidationContext(model, null, null);
            var validationResults = new List<ValidationResult>();

            Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }
        }

        private static void ValidateModelItems<T>(Controller controller, IEnumerable<T> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                ValidateModel(controller, item);
            }
        }

        public class TheReflowMethod : TestContainer
        {
            private readonly Mock<IReflowPackageService> _reflowPackageServiceMock;

            private readonly Package _availablePackage;
            private readonly Package _availablePackage2;
            private readonly Package _deletedPackage;

            public TheReflowMethod()
            {
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

                // ReflowAsync returns the package for available packages, null for deleted
                _reflowPackageServiceMock
                    .Setup(s => s.ReflowAsync(_availablePackage.Id, _availablePackage.Version, It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(_availablePackage);

                _reflowPackageServiceMock
                    .Setup(s => s.ReflowAsync(_availablePackage2.Id, _availablePackage2.Version, It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(_availablePackage2);

                // Deleted package returns null from the service (not found internally)
                _reflowPackageServiceMock
                    .Setup(s => s.ReflowAsync(_deletedPackage.Id, _deletedPackage.Version, It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync((Package)null);
            }

            [Fact]
            public async Task Returns400WhenModelStateIsInvalidAsync()
            {
                var controller = CreateController();
                controller.ModelState.AddModelError("Packages", "The packages field is required.");

                var result = await controller.ReflowPackageAsync(new AdminReflowPackageRequest()) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsNullAsync()
            {
                var request = new AdminReflowPackageRequest { Packages = null, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminReflowPackageRequest { Packages = [], Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

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

                var request = new AdminReflowPackageRequest { Packages = packages, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenReasonIsMissingAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = "A", Version = "1.0.0" }
                    ]
                };

                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenVersionIsInvalidAsync()
            {
                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = "A", Version = "not-a-version" }
                    ],
                    Reason = "test"
                };

                var controller = CreateController();
                ValidateModel(controller, request);
                ValidateModelItems(controller, request.Packages);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

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

                var controller = CreateController(reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

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
                    callerAzp: "test-app",
                    reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

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

                var controller = CreateController(reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[1].Status);
                Assert.Equal(AdminReflowPackageStatus.Accepted, response.Results[2].Status);
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
                    reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

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

                var controller = CreateController(reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.NotFound, response.Results[0].Status);
            }

            [Fact]
            public async Task ReturnsFailedStatusWhenReflowThrowsAsync()
            {
                _reflowPackageServiceMock
                    .Setup(s => s.ReflowAsync(
                        _availablePackage.Id,
                        _availablePackage.Version,
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .ThrowsAsync(new InvalidOperationException("reflow error"));

                var request = new AdminReflowPackageRequest
                {
                    Packages =
                    [
                        new AdminReflowPackageIdentity { Id = _availablePackage.Id, Version = _availablePackage.Version }
                    ],
                    Reason = "test"
                };

                var controller = CreateController(
                    reflowPackageService: _reflowPackageServiceMock);

                var result = await controller.ReflowPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminReflowPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminReflowPackageStatus.Failed, response.Results[0].Status);
            }
        }

        public class TheLockPackageMethod: TestContainer
        {
            [Fact]
            public async Task Returns400WhenModelStateIsInvalidAsync()
            {
                var controller = CreateController();
                controller.ModelState.AddModelError("Packages", "The packages field is required.");

                var result = await controller.LockPackageAsync(new AdminLockPackageRequest()) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsNullAsync()
            {
                var request = new AdminLockPackageRequest { Packages = null, Locked = true, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminLockPackageRequest { Packages = [], Locked = true, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackageEntryIsNullAsync()
            {
                var request = new AdminLockPackageRequest
                {
                    Packages = [null],
                    Locked = true,
                    Reason = "test"
                };

                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockPackageAsync(request) as JsonResult;

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

                var request = new AdminLockPackageRequest { Packages = packages, Locked = true, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenLockedIsMissingAsync()
            {
                var request = new AdminLockPackageRequest
                {
                    Packages = [new AdminLockPackageIdentity { Id = "My.Package" }],
                    Reason = "test"
                };

                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenReasonIsMissingAsync()
            {
                var request = new AdminLockPackageRequest
                {
                    Packages = [new AdminLockPackageIdentity { Id = "My.Package" }],
                    Locked = true
                };

                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
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
                    Locked = true,
                    Reason = "test lock"
                };

                var controller = CreateController(
                    callerAzp: "test-app",
                    lockPackageService: lockPackageService);

                var result = await controller.LockPackageAsync(request) as JsonResult;

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
                    Locked = true,
                    Reason = "dedupe test"
                };

                var controller = CreateController(lockPackageService: lockPackageService);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockPackageResponse>(result);
                Assert.Equal(2, response.Results.Count);

                lockPackageService.Verify(
                    s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(2));
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

                var controller = CreateController(lockPackageService: lockPackageService);

                await controller.LockPackageAsync(request);

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
                    Locked = true,
                    Reason = "security incident"
                };

                var controller = CreateController(
                    callerAzp: "my-service-principal",
                    lockPackageService: lockPackageService);

                await controller.LockPackageAsync(request);

                lockPackageService.Verify(
                    s => s.SetLockStateAsync("My.Package", true, "security incident", "my-service-principal"),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsNotFoundWhenPackageDoesNotExistAsync()
            {
                var lockPackageService = new Mock<ILockPackageService>();
                lockPackageService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockPackageServiceResult.PackageNotFound);

                var request = new AdminLockPackageRequest
                {
                    Packages = [new AdminLockPackageIdentity { Id = "DoesNotExist" }],
                    Locked = true,
                    Reason = "test"
                };

                var controller = CreateController(lockPackageService: lockPackageService);

                var result = await controller.LockPackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockPackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminLockPackageStatus.NotFound, response.Results[0].Status);
            }
        }

        public class TheLockUserMethod : TestContainer
        {
            [Fact]
            public async Task Returns400WhenModelStateIsInvalidAsync()
            {
                var controller = CreateController();
                controller.ModelState.AddModelError("Users", "The users field is required.");

                var result = await controller.LockUserAsync(new AdminLockUserRequest()) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenUsersIsNullAsync()
            {
                var request = new AdminLockUserRequest { Users = null, Locked = true, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockUserAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenUsersIsEmptyAsync()
            {
                var request = new AdminLockUserRequest { Users = [], Locked = true, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockUserAsync(request) as JsonResult;

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

                var request = new AdminLockUserRequest { Users = users, Locked = true, Reason = "test" };
                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockUserAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenUserEntryIsNullAsync()
            {
                var request = new AdminLockUserRequest
                {
                    Users = [null],
                    Locked = true,
                    Reason = "test"
                };

                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockUserAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenLockedIsMissingAsync()
            {
                var request = new AdminLockUserRequest
                {
                    Users = [new AdminUserIdentity { Username = "testuser" }],
                    Reason = "test"
                };

                var controller = CreateController();
                ValidateModel(controller, request);

                var result = await controller.LockUserAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
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
                    Locked = true,
                    Reason = "TOS violation"
                };

                var controller = CreateController(
                    callerAzp: "test-app",
                    lockUserService: lockUserService);

                var result = await controller.LockUserAsync(request) as JsonResult;

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
                    Locked = true,
                    Reason = "dedupe test"
                };

                var controller = CreateController(lockUserService: lockUserService);

                var result = await controller.LockUserAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockUserResponse>(result);
                Assert.Equal(2, response.Results.Count);

                lockUserService.Verify(
                    s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(2));
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

                var controller = CreateController(lockUserService: lockUserService);

                await controller.LockUserAsync(request);

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
                    Locked = true,
                    Reason = "TOS violation"
                };

                var controller = CreateController(
                    callerAzp: "my-service-principal",
                    lockUserService: lockUserService);

                await controller.LockUserAsync(request);

                lockUserService.Verify(
                    s => s.SetLockStateAsync("testuser", true, "TOS violation", "my-service-principal"),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsNotFoundWhenUserDoesNotExistAsync()
            {
                var lockUserService = new Mock<ILockUserService>();
                lockUserService
                    .Setup(s => s.SetLockStateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(LockUserServiceResult.UserNotFound);

                var request = new AdminLockUserRequest
                {
                    Users = [new AdminUserIdentity { Username = "nonexistentuser" }],
                    Locked = true,
                    Reason = "test"
                };

                var controller = CreateController(lockUserService: lockUserService);

                var result = await controller.LockUserAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminLockUserResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminLockUserStatus.NotFound, response.Results[0].Status);
            }
        }

        public class TheSoftDeletePackageMethod : TestContainer
        {
            private readonly Mock<IPackageService> _packageServiceMock;
            private readonly Mock<IPackageDeleteService> _packageDeleteServiceMock;
            private readonly Mock<IFeatureFlagService> _featureFlagServiceMock;

            private readonly Package _availablePackage;
            private readonly Package _availablePackage2;
            private readonly Package _deletedPackage;
            private readonly Package _multiVersionPackageV1;
            private readonly Package _multiVersionPackageV2;
            private readonly Package _multiVersionPackageV3Deleted;

            public TheSoftDeletePackageMethod()
            {
                _packageServiceMock = new Mock<IPackageService>();
                _packageDeleteServiceMock = new Mock<IPackageDeleteService>();
                _featureFlagServiceMock = new Mock<IFeatureFlagService>();
                _featureFlagServiceMock.Setup(f => f.IsAdminApiSoftDeleteEnabled()).Returns(true);

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

                _multiVersionPackageV1 = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Multi.Version" },
                    Version = "1.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                _multiVersionPackageV2 = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Multi.Version" },
                    Version = "2.0.0",
                    PackageStatusKey = PackageStatus.Available
                };

                _multiVersionPackageV3Deleted = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Multi.Version" },
                    Version = "3.0.0",
                    PackageStatusKey = PackageStatus.Deleted
                };

                SetupPackages(_packageServiceMock, [_availablePackage, _availablePackage2, _deletedPackage,
                    _multiVersionPackageV1, _multiVersionPackageV2, _multiVersionPackageV3Deleted]);

                _packageServiceMock
                    .Setup(s => s.FindPackagesById("Multi.Version", PackageDeprecationFieldsToInclude.None))
                    .Returns([_multiVersionPackageV1, _multiVersionPackageV2, _multiVersionPackageV3Deleted]);
            }

            private AdminApiController CreateSoftDeleteController(
                string callerAzp = null,
                Mock<IPackageService> packageService = null,
                Mock<IPackageDeleteService> packageDeleteService = null)
            {
                return CreateController(
                    callerAzp: callerAzp,
                    packageService: packageService,
                    packageDeleteService: packageDeleteService,
                    featureFlagService: _featureFlagServiceMock);
            }

            [Fact]
            public async Task Returns400WhenModelStateIsInvalidAsync()
            {
                var controller = CreateSoftDeleteController();
                controller.ModelState.AddModelError("Packages", "The packages field is required.");

                var result = await controller.SoftDeletePackageAsync(new AdminSoftDeletePackageRequest()) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsNullAsync()
            {
                var request = new AdminSoftDeletePackageRequest { Packages = null, Reason = "test" };
                var controller = CreateSoftDeleteController();
                ValidateModel(controller, request);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackagesIsEmptyAsync()
            {
                var request = new AdminSoftDeletePackageRequest { Packages = [], Reason = "test" };
                var controller = CreateSoftDeleteController();
                ValidateModel(controller, request);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenPackageEntryIsNullAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages = [null],
                    Reason = "test"
                };

                var controller = CreateSoftDeleteController();
                ValidateModel(controller, request);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

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

                var request = new AdminSoftDeletePackageRequest { Packages = packages, Reason = "test" };
                var controller = CreateSoftDeleteController();
                ValidateModel(controller, request);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenReasonIsMissingAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "A", Version = "1.0.0" }
                    ]
                };

                var controller = CreateSoftDeleteController();
                ValidateModel(controller, request);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
            }

            [Fact]
            public async Task Returns400WhenVersionIsInvalidAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "A", Version = "not-a-version" }
                    ],
                    Reason = "test"
                };

                var controller = CreateSoftDeleteController();
                ValidateModel(controller, request);
                ValidateModelItems(controller, request.Packages);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

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

                var controller = CreateSoftDeleteController(packageService: _packageServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

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

                var controller = CreateSoftDeleteController(
                    callerAzp: "test-app",
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

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

                var controller = CreateSoftDeleteController(
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[1].Status);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[2].Status);
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

                var controller = CreateSoftDeleteController(
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(2, response.Results.Count);
                Assert.All(response.Results, r => Assert.Equal(AdminSoftDeletePackageStatus.Accepted, r.Status));
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

                var controller = CreateSoftDeleteController(packageService: _packageServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[0].Status);
            }

            [Fact]
            public async Task WildcardDeletesAllAvailableVersionsAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "Multi.Version", Version = "*" }
                    ],
                    Reason = "wildcard test"
                };

                var controller = CreateSoftDeleteController(
                    callerAzp: "test-app",
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Equal(3, response.Results.Count);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal("1.0.0", response.Results[0].Version);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[1].Status);
                Assert.Equal("2.0.0", response.Results[1].Version);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[2].Status);
                Assert.Equal("3.0.0", response.Results[2].Version);

                _packageDeleteServiceMock.Verify(
                    s => s.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => new List<Package>(p).Count == 2),
                        null,
                        "wildcard test",
                        "test-app"),
                    Times.Once);
            }

            [Fact]
            public async Task WildcardReturns400WhenPackageIdNotFoundAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "DoesNotExist", Version = "*" }
                    ],
                    Reason = "wildcard not found"
                };

                var controller = CreateSoftDeleteController(packageService: _packageServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                Assert.Single(response.Results);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[0].Status);
                Assert.Equal("*", response.Results[0].Version);
            }

            [Fact]
            public async Task WildcardDeduplicatesWithExplicitVersionAsync()
            {
                var request = new AdminSoftDeletePackageRequest
                {
                    Packages =
                    [
                        new AdminSoftDeletePackageIdentity { Id = "Multi.Version", Version = "1.0.0" },
                        new AdminSoftDeletePackageIdentity { Id = "Multi.Version", Version = "*" }
                    ],
                    Reason = "dedup wildcard"
                };

                var controller = CreateSoftDeleteController(
                    packageService: _packageServiceMock,
                    packageDeleteService: _packageDeleteServiceMock);

                var result = await controller.SoftDeletePackageAsync(request) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Accepted, controller.Response.StatusCode);

                var response = GetResponseData<AdminSoftDeletePackageResponse>(result);
                // 1.0.0 from the explicit entry, then 2.0.0 and 3.0.0 from the wildcard (1.0.0 is deduped)
                Assert.Equal(3, response.Results.Count);
                Assert.Equal("1.0.0", response.Results[0].Version);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[0].Status);
                Assert.Equal("2.0.0", response.Results[1].Version);
                Assert.Equal(AdminSoftDeletePackageStatus.Accepted, response.Results[1].Status);
                Assert.Equal("3.0.0", response.Results[2].Version);
                Assert.Equal(AdminSoftDeletePackageStatus.NotFound, response.Results[2].Status);
            }

            [Fact]
            public void WildcardVersionPassesValidation()
            {
                var identity = new AdminSoftDeletePackageIdentity { Id = "Test", Version = "*" };
                var context = new ValidationContext(identity);
                var results = new List<ValidationResult>();

                var isValid = Validator.TryValidateObject(identity, context, results, validateAllProperties: true);

                Assert.True(isValid);
                Assert.Empty(results);
            }
        }
    }
}
