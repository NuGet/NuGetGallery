// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class StagingApiControllerFacts : TestContainer
    {
        [Fact]
        public void ControllerRequiresApiAuthorization()
        {
            Assert.NotEmpty(typeof(StagingApiController).GetCustomAttributes(typeof(ApiAuthorizeAttribute), inherit: true));
            Assert.NotEmpty(typeof(StagingApiController).GetCustomAttributes(typeof(ApiScopeRequiredAttribute), inherit: true));
        }

        [Fact]
        public async Task CreatesStagingGroupForApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var created = new DateTime(2026, 9, 11, 20, 0, 0, DateTimeKind.Utc);
            var group = new StagingGroup
            {
                Id = "net10-preview",
                Name = ".NET 10 Preview",
                Owner = owner,
                OwnerKey = owner.Key,
                CreatedDate = created,
            };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupWithApiKeyAsync(
                    currentUser,
                    It.Is<IEnumerable<Scope>>(scopes => scopes.Single().OwnerKey == owner.Key),
                    "net10-preview",
                    ".NET 10 Preview"))
                .ReturnsAsync(CreateStagingGroupResult.Created(group));

            var result = await target.CreateStagingGroup(new CreateStagingGroupRequest
            {
                Id = "net10-preview",
                Name = "  .NET 10 Preview  ",
            });

            var content = Assert.IsType<ContentResult>(result);
            Mock.Get(target.Response).VerifySet(x => x.StatusCode = (int)HttpStatusCode.Created);
            Assert.Equal("application/json", content.ContentType);
            JObject body;
            using (var reader = new JsonTextReader(new StringReader(content.Content)) { DateParseHandling = DateParseHandling.None })
            {
                body = JObject.Load(reader);
            }
            Assert.Equal("net10-preview", (string)body["id"]);
            Assert.Equal(".NET 10 Preview", (string)body["name"]);
            Assert.Equal("example-org", (string)body["owner"]);
            Assert.Equal("2026-09-11T20:00:00.0000000Z", (string)body["created"]);
            Assert.Equal("2026-10-11T20:00:00.0000000Z", (string)body["expires"]);
            Assert.Equal(0, (int)body["itemCount"]);
            Assert.False((bool)body["canPromote"]);
            Assert.Equal("GroupEmpty", (string)body["blockers"][0]["code"]);
            Assert.EndsWith("/account/staging/example-org/groups/net10-preview", (string)body["managementUrl"]);
        }

        [Fact]
        public async Task RejectsUnsupportedCreateGroupContentType()
        {
            var target = GetController<StagingApiController>();
            var httpContext = TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), target);
            var request = Mock.Get(httpContext.Object.Request);
            request.SetupGet(x => x.ContentType).Returns("text/plain");

            var result = await target.CreateStagingGroup(new CreateStagingGroupRequest
            {
                Id = "release",
                Name = "Release",
            });

            AssertError(target, result, HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.CreateStagingGroupWithApiKeyAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Scope>>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        public static IEnumerable<object[]> InvalidCreateGroupRequests
        {
            get
            {
                yield return new object[] { null, "InvalidJson", null };
                yield return new object[] { new CreateStagingGroupRequest { Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "invalid id", Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "release" }, "InvalidRequest", "name" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "release", Name = "   " }, "InvalidRequest", "name" };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidCreateGroupRequests))]
        public async Task RejectsInvalidCreateGroupRequest(CreateStagingGroupRequest request, string errorCode, string errorTarget)
        {
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, new User("current") { Key = 1 }, owner: null);
            AddModelErrors(target, request);

            var result = await target.CreateStagingGroup(request);

            AssertError(target, result, HttpStatusCode.BadRequest, errorCode, errorTarget);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.CreateStagingGroupWithApiKeyAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Scope>>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task RejectsCreateGroupWithoutApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupWithApiKeyAsync(currentUser, It.IsAny<IEnumerable<Scope>>(), "release", "Release"))
                .ReturnsAsync(CreateStagingGroupResult.OwnerNotFound());

            var result = await target.CreateStagingGroup(new CreateStagingGroupRequest { Id = "release", Name = "Release" });

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
        }

        [Fact]
        public async Task RejectsDuplicateStagingGroupId()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupWithApiKeyAsync(currentUser, It.IsAny<IEnumerable<Scope>>(), "release", "Release"))
                .ReturnsAsync(CreateStagingGroupResult.GroupAlreadyExists());

            var result = await target.CreateStagingGroup(new CreateStagingGroupRequest { Id = "release", Name = "Release" });

            AssertError(target, result, HttpStatusCode.Conflict, "GroupAlreadyExists", "id");
        }

        [Fact]
        public void GetsStagedPackages()
        {
            var currentUser = new User("current") { Key = 1 };
            var packages = new[] { new PackageStagingStatus { Id = "PackageA" } };
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetPackages(currentUser, It.IsAny<IEnumerable<Scope>>()))
                .Returns(packages);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = target.GetStagedPackages();

            var json = Assert.IsType<JsonResult>(result);
            Assert.Same(packages, json.Data);
        }

        [Fact]
        public async Task DownloadsAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            var content = new MemoryStream();
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(stagedPackage))
                .ReturnsAsync(content);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadStagedPackage("PackageA", "1.0.0");

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(CoreConstants.PackageContentType, file.ContentType);
            Assert.Equal("PackageA.1.0.0.nupkg", file.FileDownloadName);
        }

        [Fact]
        public async Task HidesUnauthorizedPackageDownload()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(false);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadStagedPackage("PackageA", "1.0.0");

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(404, status.StatusCode);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.OpenPackageContentAsync(It.IsAny<StagedPackage>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdatesListedIntentForAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            var status = new PackageStagingStatus { Listed = true };
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.UpdateListedAsync(stagedPackage, true))
                .Returns(Task.CompletedTask);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStatus(stagedPackage))
                .Returns(status);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.UpdateStagedPackageListed(
                "PackageA",
                "1.0.0",
                new UpdateStagedPackageRequest { Listed = true });

            var json = Assert.IsType<JsonResult>(result);
            Assert.Same(status, json.Data);
        }

        [Fact]
        public async Task RejectsMissingUpdateRequest()
        {
            var target = GetController<StagingApiController>();

            var result = await target.UpdateStagedPackageListed("PackageA", "1.0.0", request: null);

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(400, status.StatusCode);
        }

        [Fact]
        public async Task HidesUnauthorizedPackageUpdate()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(false);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.UpdateStagedPackageListed(
                "PackageA",
                "1.0.0",
                new UpdateStagedPackageRequest { Listed = true });

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(404, status.StatusCode);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.UpdateListedAsync(It.IsAny<StagedPackage>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletesAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeletePackageAsync(stagedPackage))
                .Returns(Task.CompletedTask);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeleteStagedPackage("PackageA", "1.0.0");

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(204, status.StatusCode);
        }

        private static void ConfigureCreateGroupRequest(StagingApiController target, User currentUser, User owner)
        {
            var httpContext = TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), target);
            if (owner == null)
            {
                target.SetCurrentUser(currentUser);
            }
            else
            {
                target.SetCurrentUser(
                    currentUser,
                    new[]
                    {
                        new Scope(owner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush)
                        {
                            OwnerKey = owner.Key,
                        },
                    });
            }

            var request = Mock.Get(httpContext.Object.Request);
            request.SetupGet(x => x.ContentType).Returns("application/json; charset=utf-8");
        }

        private static void AddModelErrors(Controller controller, object model)
        {
            if (model == null)
            {
                return;
            }

            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true);
            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }
        }

        private static void AssertError(StagingApiController controller, ActionResult result, HttpStatusCode statusCode, string code, string target = null)
        {
            var json = Assert.IsType<JsonResult>(result);
            Mock.Get(controller.Response).VerifySet(x => x.StatusCode = (int)statusCode);
            var body = JObject.FromObject(json.Data);
            Assert.Equal(code, (string)body["error"]["code"]);
            if (target != null)
            {
                Assert.Equal(target, (string)body["error"]["target"]);
            }
        }

        private static StagedPackage CreateStagedPackage(User owner)
        {
            var package = new Package
            {
                Key = 42,
                NormalizedVersion = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "PackageA" },
            };
            var identity = new StagedPackageIdentity
            {
                Key = package.Key,
                Package = package,
                OwnerKey = owner.Key,
                Owner = owner,
            };
            var stagedPackage = new StagedPackage
            {
                Key = 43,
                StagedPackageIdentityKey = identity.Key,
                StagedPackageIdentity = identity,
            };
            identity.CurrentStagedPackageKey = stagedPackage.Key;
            identity.CurrentStagedPackage = stagedPackage;
            return stagedPackage;
        }
    }
}
