// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
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
        public async Task StagesMultipartPackageWithGroupAndListingIntent()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            var request = Mock.Get(target.Request);
            request.SetupGet(x => x.ContentType).Returns("multipart/form-data; boundary=test");
            request.SetupGet(x => x.Form).Returns(new NameValueCollection { { "groupId", "release" }, { "listed", "false" } });
            var files = new Mock<HttpFileCollectionBase>();
            files.SetupGet(x => x.Count).Returns(1);
            files.Setup(x => x.GetKey(0)).Returns("package");
            request.SetupGet(x => x.Files).Returns(files.Object);
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var file = new Mock<HttpPostedFileBase>();
            file.SetupGet(x => x.InputStream).Returns(stream);
            GetMock<IPackageStagingUploadService>()
                .Setup(x => x.StagePackageAsync(currentUser, It.IsAny<IReadOnlyCollection<Scope>>(), target.HttpContext, stream, "release", false))
                .ReturnsAsync(PackageStagingResult.Ok());

            var result = await target.StagePackage(new StagePackageRequest { Package = file.Object, GroupId = "release", Listed = false });

            Assert.Equal((int)HttpStatusCode.OK, Assert.IsType<HttpStatusCodeWithServerWarningResult>(result).StatusCode);
            GetMock<IPackageStagingUploadService>().Verify(
                x => x.StagePackageAsync(currentUser, It.IsAny<IReadOnlyCollection<Scope>>(), target.HttpContext, stream, "release", false),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("release")]
        public async Task StagesMultipartSymbolPackage(string groupId)
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);
            var request = Mock.Get(target.Request);
            request.SetupGet(x => x.ContentType).Returns("multipart/form-data; boundary=test");
            var files = new Mock<HttpFileCollectionBase>();
            files.SetupGet(x => x.Count).Returns(1);
            files.Setup(x => x.GetKey(0)).Returns("package");
            request.SetupGet(x => x.Files).Returns(files.Object);
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var file = new Mock<HttpPostedFileBase>();
            file.SetupGet(x => x.InputStream).Returns(stream);
            GetMock<ISymbolPackageStagingUploadService>()
                .Setup(x => x.StageSymbolPackageAsync(currentUser, It.IsAny<IReadOnlyCollection<Scope>>(), target.HttpContext, stream, groupId))
                .ReturnsAsync(PackageStagingResult.Created(warnings: null));

            var result = await target.StageSymbolPackage(new StageSymbolPackageRequest { Package = file.Object, GroupId = groupId });

            Assert.Equal((int)HttpStatusCode.Created, Assert.IsType<HttpStatusCodeWithServerWarningResult>(result).StatusCode);
            GetMock<ISymbolPackageStagingUploadService>().Verify(
                x => x.StageSymbolPackageAsync(currentUser, It.IsAny<IReadOnlyCollection<Scope>>(), target.HttpContext, stream, groupId),
                Times.Once);
        }

        [Theory]
        [InlineData("application/octet-stream", "UnsupportedMediaType")]
        [InlineData("multipart/form-data; boundary=test", "InvalidRequest")]
        public async Task RejectsUploadWithoutMultipartPackage(string contentType, string errorCode)
        {
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, new User("current") { Key = 1 }, owner: null);
            var request = Mock.Get(target.Request);
            request.SetupGet(x => x.ContentType).Returns(contentType);

            var result = await target.StagePackage(new StagePackageRequest());

            AssertError(target, result, contentType == "application/octet-stream" ? HttpStatusCode.UnsupportedMediaType : HttpStatusCode.BadRequest, errorCode);
            GetMock<IPackageStagingUploadService>().Verify(
                x => x.StagePackageAsync(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<HttpContextBase>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool?>()),
                Times.Never);
        }

        [Fact]
        public async Task RejectsEmptyGroupIdBeforeUpload()
        {
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, new User("current") { Key = 1 }, owner: null);
            var request = Mock.Get(target.Request);
            request.SetupGet(x => x.ContentType).Returns("multipart/form-data; boundary=test");
            request.SetupGet(x => x.Form).Returns(new NameValueCollection { { "groupId", "" } });
            var files = new Mock<HttpFileCollectionBase>();
            files.SetupGet(x => x.Count).Returns(1);
            files.Setup(x => x.GetKey(0)).Returns("package");
            request.SetupGet(x => x.Files).Returns(files.Object);

            var result = await target.StagePackage(new StagePackageRequest { Package = Mock.Of<HttpPostedFileBase>() });

            AssertError(target, result, HttpStatusCode.BadRequest, "InvalidRequest", "groupid");
        }

        [Theory]
        [InlineData("bad group")]
        [InlineData("x.")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public async Task RejectsMalformedGroupIdBeforeUpload(string groupId)
        {
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, new User("current") { Key = 1 }, owner: null);
            var request = Mock.Get(target.Request);
            request.SetupGet(x => x.ContentType).Returns("multipart/form-data; boundary=test");
            request.SetupGet(x => x.Form).Returns(new NameValueCollection { { "groupId", groupId } });
            var files = new Mock<HttpFileCollectionBase>();
            files.SetupGet(x => x.Count).Returns(1);
            files.Setup(x => x.GetKey(0)).Returns("package");
            request.SetupGet(x => x.Files).Returns(files.Object);
            target.ModelState.AddModelError("GroupId", "Invalid group ID.");

            var result = await target.StagePackage(new StagePackageRequest { Package = Mock.Of<HttpPostedFileBase>(), GroupId = groupId });

            AssertError(target, result, HttpStatusCode.BadRequest, "InvalidRequest", "groupid");
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
                ExpirationDate = new DateTime(2026, 10, 25, 20, 0, 0, DateTimeKind.Utc),
            };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupAsync(owner, "net10-preview", ".NET 10 Preview"))
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
            Assert.Equal("2026-10-25T20:00:00.0000000Z", (string)body["expires"]);
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
                x => x.CreateStagingGroupAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        public static IEnumerable<object[]> InvalidCreateGroupRequests
        {
            get
            {
                yield return new object[] { null, "InvalidJson", null };
                yield return new object[] { new CreateStagingGroupRequest { Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "invalid id", Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = ".", Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "..", Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "-release", Name = "Release" }, "InvalidRequest", "id" };
                yield return new object[] { new CreateStagingGroupRequest { Id = "release_", Name = "Release" }, "InvalidRequest", "id" };
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
                x => x.CreateStagingGroupAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task RejectsCreateGroupWithoutApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = await target.CreateStagingGroup(new CreateStagingGroupRequest { Id = "release", Name = "Release" });

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.CreateStagingGroupAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task RejectsDuplicateStagingGroupId()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupAsync(owner, "release", "Release"))
                .ReturnsAsync(CreateStagingGroupResult.GroupAlreadyExists());

            var result = await target.CreateStagingGroup(new CreateStagingGroupRequest { Id = "release", Name = "Release" });

            AssertError(target, result, HttpStatusCode.Conflict, "GroupAlreadyExists", "id");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [InlineData(true, PackageStatus.Deleted)]
        public void GetsPagedStagingGroups(bool withSymbols, PackageStatus parentStatus = PackageStatus.Available)
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var olderGroup = CreateStagingGroup(10, "older", "Older", owner, new DateTime(2026, 9, 1));
            var newerGroup = CreateStagingGroup(11, "newer", "Newer", owner, new DateTime(2026, 9, 2));
            var parent = CreateStagedPackage(owner);
            var identity = parent.StagedPackageIdentity;
            identity.Package.PackageStatusKey = parentStatus;
            identity.CurrentStagedPackageKey = null;
            identity.CurrentStagedPackage = null;
            identity.StagingGroupKey = newerGroup.Key;
            identity.StagingGroup = newerGroup;
            var symbols = new StagedSymbolPackage
            {
                Key = 50,
                StagedPackageIdentity = identity,
                StagedPackageIdentityKey = identity.Key,
                SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged },
                Status = StagedPackageStatus.Ready,
            };
            identity.CurrentStagedSymbolPackageKey = symbols.Key;
            var groupSymbols = Array.Empty<StagedSymbolPackage>();
            if (withSymbols)
            {
                groupSymbols = new[] { symbols };
            }

            var summary = new StagingGroupSummary(newerGroup, Array.Empty<StagedPackage>(), groupSymbols);
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupSummaryPage(owner, 1, 1))
                .Returns(new StagingGroupSummaryPage(new[] { summary }, totalCount: 2));

            var result = target.GetStagingGroups(page: 1, pageSize: 1);

            var body = ParseJsonContent(result);
            Assert.Equal(1, (int)body["page"]);
            Assert.Equal(1, (int)body["pageSize"]);
            Assert.Equal(2, (int)body["totalCount"]);
            Assert.Equal("newer", (string)body["items"][0]["id"]);
            Assert.Equal(withSymbols && parentStatus == PackageStatus.Available, (bool)body["items"][0]["canPromote"]);
            Assert.EndsWith("Z", (string)body["items"][0]["created"]);
            Assert.EndsWith("Z", (string)body["items"][0]["expires"]);
        }

        [Fact]
        public void GetsEmptyStagingGroupPageBeyondTheEnd()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var group = CreateStagingGroup(10, "release", "Release", owner, new DateTime(2026, 9, 1));
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupSummaryPage(owner, 2, 100))
                .Returns(new StagingGroupSummaryPage(Array.Empty<StagingGroupSummary>(), totalCount: 1));

            var result = target.GetStagingGroups(page: 2, pageSize: 100);

            var body = ParseJsonContent(result);
            Assert.Empty(body["items"]);
            Assert.Equal(1, (int)body["totalCount"]);
        }

        [Fact]
        public void RejectsGroupListWithoutApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = target.GetStagingGroups();

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [InlineData(true, StagedPackageStatus.WaitingForParent)]
        public void GetsStagingGroupWithPagedMembers(bool withSymbols, StagedPackageStatus symbolStatus = StagedPackageStatus.Ready)
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var group = CreateStagingGroup(10, "release", "Release", owner, new DateTime(2026, 9, 1));
            var package = CreateStagedPackage(owner);
            package.Status = StagedPackageStatus.Ready;
            package.UploadedDate = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
            package.StagedPackageIdentity.StagingGroupKey = group.Key;
            package.StagedPackageIdentity.StagingGroup = group;
            var symbols = new StagedSymbolPackage { Key = 50, StagedPackageIdentity = package.StagedPackageIdentity, SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged }, Status = symbolStatus, UploadedDate = package.UploadedDate.AddMinutes(-1) };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            var groupSymbols = Array.Empty<StagedSymbolPackage>();
            if (withSymbols)
            {
                groupSymbols = new[] { symbols };
            }

            var itemCount = 1 + groupSymbols.Length;
            var allReady = !withSymbols || symbolStatus == StagedPackageStatus.Ready;
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupPackagePage(owner, "RELEASE", 1, 100))
                .Returns(new StagingGroupPackagePage(group, new[] { package }, itemCount, allReady, groupSymbols, groupSymbols.Length));

            var result = target.GetStagingGroup("RELEASE");

            var body = ParseJsonContent(result);
            Assert.Equal("release", (string)body["group"]["id"]);
            Assert.Equal(withSymbols ? 2 : 1, (int)body["group"]["itemCount"]);
            Assert.Equal(!withSymbols || symbolStatus == StagedPackageStatus.Ready, (bool)body["group"]["canPromote"]);
            if (withSymbols)
            {
                if (symbolStatus != StagedPackageStatus.Ready)
                {
                    Assert.Equal("GroupNotReady", (string)body["group"]["blockers"][0]["code"]);
                }
                else
                {
                    Assert.Empty(body["group"]["blockers"]);
                }

                Assert.Equal("symbols", (string)body["items"][1]["kind"]);
                Assert.Equal(symbolStatus == StagedPackageStatus.WaitingForParent ? "waitingForParent" : "ready", (string)body["items"][1]["status"]);
                if (symbolStatus == StagedPackageStatus.WaitingForParent)
                {
                    Assert.Equal("ParentPackageMissing", (string)body["items"][1]["blockers"][0]["code"]);
                }
                Assert.Null(body["items"][1]["listed"]);
                Assert.False((bool)body["items"][1]["canPromote"]);
                Assert.Equal("release", (string)body["items"][1]["group"]["id"]);
            }
            else
            {
                Assert.Empty(body["group"]["blockers"]);
            }

            Assert.Equal(withSymbols ? 2 : 1, (int)body["totalCount"]);
            Assert.Equal("PackageA", (string)body["items"][0]["id"]);
            Assert.Equal("package", (string)body["items"][0]["kind"]);
            Assert.Equal("ready", (string)body["items"][0]["status"]);
            Assert.Equal("release", (string)body["items"][0]["group"]["id"]);
            Assert.Null(body["items"][0]["validated"].Value<string>());
            Assert.Equal((string)body["group"]["expires"], (string)body["items"][0]["expires"]);
        }

        [Theory]
        [InlineData("RegistrationOwnershipLost")]
        [InlineData("GroupPromotionInProgress")]
        [InlineData("StagingExpired")]
        public void ReportsOffPageOwnershipLossWithExistingGroupBlockerPrecedence(string expectedBlocker)
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var group = CreateStagingGroup(10, "release", "Release", owner, DateTime.UtcNow);
            if (expectedBlocker == "GroupPromotionInProgress")
            {
                group.ActivePromotionId = Guid.NewGuid();
            }
            else if (expectedBlocker == "StagingExpired")
            {
                group.ExpirationDate = DateTime.UtcNow.AddMinutes(-1);
            }

            var package = CreateStagedPackage(owner);
            package.Status = StagedPackageStatus.Ready;
            package.StagedPackageIdentity.StagingGroupKey = group.Key;
            package.StagedPackageIdentity.StagingGroup = group;
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupPackagePage(owner, "release", 1, 1))
                .Returns(new StagingGroupPackagePage(group, new[] { package }, totalCount: 2, allPackagesReady: false, hasRegistrationOwnershipLoss: true));

            var body = ParseJsonContent(target.GetStagingGroup("release", page: 1, pageSize: 1));

            Assert.False((bool)body["group"]["canPromote"]);
            Assert.Equal(expectedBlocker, (string)Assert.Single(body["group"]["blockers"])["code"]);
            Assert.Single(body["items"]);
            Assert.DoesNotContain(body["items"][0]["blockers"], blocker => (string)blocker["code"] == "RegistrationOwnershipLost");
            if (expectedBlocker == "RegistrationOwnershipLost")
            {
                Assert.Contains("Restore registration ownership", (string)body["group"]["blockers"][0]["message"]);
            }
        }

        [Fact]
        public void ReportsActiveGroupProgress()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var group = CreateStagingGroup(10, "release", "Release", owner, new DateTime(2026, 9, 1));
            group.ActivePromotionId = Guid.NewGuid();
            var package = CreateStagedPackage(owner);
            package.Status = StagedPackageStatus.Succeeded;
            package.StagedPackageIdentity.StagingGroupKey = group.Key;
            package.StagedPackageIdentity.StagingGroup = group;
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupPackagePage(owner, group.Id, 1, 100))
                .Returns(new StagingGroupPackagePage(group, new[] { package }, totalCount: 1, allPackagesReady: false));

            var result = target.GetStagingGroup(group.Id);

            var body = ParseJsonContent(result);
            Assert.False((bool)body["group"]["canPromote"]);
            Assert.Equal("GroupPromotionInProgress", (string)body["group"]["blockers"][0]["code"]);
            Assert.Equal("succeeded", (string)body["items"][0]["status"]);
        }

        [Fact]
        public void HidesUnavailableStagingGroup()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupPackagePage(owner, "missing", 1, 100))
                .Returns((StagingGroupPackagePage)null);

            var result = target.GetStagingGroup("missing");

            AssertError(target, result, HttpStatusCode.NotFound, "GroupNotFound");
        }

        [Theory]
        [InlineData(0, 100, "page")]
        [InlineData(1, 0, "pageSize")]
        [InlineData(1, 501, "pageSize")]
        public void RejectsInvalidGroupPaging(int page, int pageSize, string errorTarget)
        {
            var target = GetController<StagingApiController>();

            var result = target.GetStagingGroups(page, pageSize);

            AssertError(target, result, HttpStatusCode.BadRequest, "InvalidPaging", errorTarget);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagingGroupSummaries(It.IsAny<User>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletesOnlyAuthorizedStagedSymbols()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("owner") { Key = 2 };
            var symbol = new StagedSymbolPackage { StagedPackageIdentity = CreateStagedPackage(owner).StagedPackageIdentity };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedSymbolPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "packagea", "1.0"))
                .Returns(symbol);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.DeletePackageAsync(symbol)).ReturnsAsync(true);

            var result = await target.DeleteStagedSymbolPackage("packagea", "1.0");

            Assert.Equal((int)HttpStatusCode.NoContent, Assert.IsType<HttpStatusCodeResult>(result).StatusCode);
            GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.DeletePackageAsync(symbol), Times.Once);
            GetMock<IPackageStagingManagementService>().Verify(x => x.DeletePackageAsync(It.IsAny<StagedPackage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsSymbolDeletionWithoutEnabledApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = await target.DeleteStagedSymbolPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedSymbolPackage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.DeletePackageAsync(It.IsAny<StagedSymbolPackage>()), Times.Never);
        }

        [Fact]
        public async Task HidesUnavailableSymbolDeletionWithoutMutating()
        {
            var owner = new User("owner") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, owner, owner);

            var result = await target.DeleteStagedSymbolPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.NotFound, "SymbolPackageNotFound");
            GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.DeletePackageAsync(It.IsAny<StagedSymbolPackage>()), Times.Never);
        }

        [Fact]
        public async Task ReportsSymbolDeletionStateConflict()
        {
            var owner = new User("owner") { Key = 1 };
            var symbol = new StagedSymbolPackage { StagedPackageIdentity = CreateStagedPackage(owner).StagedPackageIdentity };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, owner, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedSymbolPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "PackageA", "1.0.0"))
                .Returns(symbol);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.DeletePackageAsync(symbol)).ReturnsAsync(false);

            var result = await target.DeleteStagedSymbolPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.Conflict, "SymbolPackageDeletionConflict");
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void GetsPagedArtifactsForApiKeyOwner(bool symbols, bool grouped)
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new Organization("owner") { Key = 2 };
            var package = CreateStagedPackage(owner);
            package.Status = StagedPackageStatus.Ready;
            package.UploadedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            if (grouped)
            {
                var group = CreateStagingGroup(10, "release", "Release", owner, package.UploadedDate);
                package.StagedPackageIdentity.StagingGroupKey = group.Key;
                package.StagedPackageIdentity.StagingGroup = group;
            }

            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            if (symbols)
            {
                package.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                var symbol = new StagedSymbolPackage
                {
                    Key = 44,
                    StagedPackageIdentity = package.StagedPackageIdentity,
                    SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged },
                    Status = StagedPackageStatus.Ready,
                    UploadedDate = package.UploadedDate,
                };
                GetMock<IPackageStagingManagementService>()
                    .Setup(x => x.GetStagedSymbolPackagePage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), 2, 1))
                    .Returns(new StagingArtifactPage<StagedSymbolPackage>(new[] { symbol }, 21));
            }
            else
            {
                GetMock<IPackageStagingManagementService>()
                    .Setup(x => x.GetStagedPackagePage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), 2, 1))
                    .Returns(new StagingArtifactPage<StagedPackage>(new[] { package }, 21));
            }

            GetMock<IStagingQuotaService>().Setup(x => x.GetUsage(owner))
                .Returns(new StagingQuotaUsage { UsedPackages = 7, UsedSymbols = 3, Limit = 42 });

            var result = symbols ? target.GetStagedSymbolPackages(2, 1) : target.GetStagedPackages(2, 1);

            var body = ParseJsonContent(result);
            Assert.Equal(2, (int)body["page"]);
            Assert.Equal(1, (int)body["pageSize"]);
            Assert.Equal(21, (int)body["totalCount"]);
            Assert.Equal(10, (int)body["quota"]["usedArtifacts"]);
            Assert.Equal(42, (int)body["quota"]["limit"]);
            Assert.Equal(2, ((JObject)body["quota"]).Count);
            var item = Assert.Single((JArray)body["items"]);
            Assert.Equal("PackageA", (string)item["id"]);
            Assert.Equal("1.0.0", (string)item["version"]);
            Assert.Equal("owner", (string)item["owner"]);
            Assert.Equal(symbols ? "symbols" : "package", (string)item["kind"]);
            Assert.Equal("ready", (string)item["status"]);
            Assert.Equal(package.UploadedDate.ToUtcIso8601String(), (string)item["uploaded"]);
            if (grouped)
            {
                Assert.Equal("release", (string)item["group"]["id"]);
            }
            else
            {
                Assert.Equal(JTokenType.Null, item["group"].Type);
            }
            Assert.Contains(grouped ? "/account/staging/owner/groups/release" : "/account/staging/owner/ungrouped", (string)item["managementUrl"]);
            if (symbols)
            {
                Assert.Null(item["listed"]);
            }
            else
            {
                Assert.NotNull(item["listed"]);
            }

            GetMock<IStagingQuotaService>().Verify(x => x.GetUsage(owner), Times.Once);
            GetMock<IStagingQuotaService>().Verify(x => x.GetUsage(currentUser), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsEmptyArtifactPageWithDefaultsAndOwnerQuota(bool symbols)
        {
            var owner = new User("owner") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, owner, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackagePage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), 1, 100))
                .Returns(new StagingArtifactPage<StagedPackage>(Array.Empty<StagedPackage>(), 0));
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedSymbolPackagePage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), 1, 100))
                .Returns(new StagingArtifactPage<StagedSymbolPackage>(Array.Empty<StagedSymbolPackage>(), 0));
            GetMock<IStagingQuotaService>().Setup(x => x.GetUsage(owner))
                .Returns(new StagingQuotaUsage { UsedPackages = 4, UsedSymbols = 1, Limit = 350 });

            var result = symbols ? target.GetStagedSymbolPackages() : target.GetStagedPackages();

            var body = ParseJsonContent(result);
            Assert.Empty((JArray)body["items"]);
            Assert.Equal(1, (int)body["page"]);
            Assert.Equal(100, (int)body["pageSize"]);
            Assert.Equal(0, (int)body["totalCount"]);
            Assert.Equal(5, (int)body["quota"]["usedArtifacts"]);
        }

        [Theory]
        [InlineData(false, 0, 100, "page")]
        [InlineData(true, 1, 501, "pageSize")]
        public void RejectsInvalidArtifactPaging(bool symbols, int page, int pageSize, string errorTarget)
        {
            var target = GetController<StagingApiController>();

            var result = symbols ? target.GetStagedSymbolPackages(page, pageSize) : target.GetStagedPackages(page, pageSize);

            AssertError(target, result, HttpStatusCode.BadRequest, "InvalidPaging", errorTarget);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedPackagePage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedSymbolPackagePage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void RejectsMalformedArtifactPaging()
        {
            var target = GetController<StagingApiController>();
            target.ModelState.AddModelError("page", "The paging value is not an integer.");

            var result = target.GetStagedPackages();

            AssertError(target, result, HttpStatusCode.BadRequest, "InvalidPaging", "page");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RejectsArtifactListWithoutEnabledApiKeyOwner(bool symbols)
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = symbols ? target.GetStagedSymbolPackages() : target.GetStagedPackages();

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
            GetMock<IStagingQuotaService>().Verify(x => x.GetUsage(It.IsAny<User>()), Times.Never);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedPackagePage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedSymbolPackagePage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Theory]
        [InlineData("GET", nameof(StagingApiController.GetStagedSymbolPackages))]
        [InlineData("PUT", nameof(StagingApiController.StageSymbolPackage))]
        public void MapsSymbolInventoryWithoutChangingSymbolUploadRoute(string method, string action)
        {
            var routes = new RouteCollection();
            Routes.RegisterStagingApiRoutes(routes);
            var context = new Mock<HttpContextBase>();
            context.SetupGet(x => x.Request.AppRelativeCurrentExecutionFilePath).Returns("~/api/v3/staging/symbols");
            context.SetupGet(x => x.Request.PathInfo).Returns(string.Empty);
            context.SetupGet(x => x.Request.HttpMethod).Returns(method);

            var route = routes.GetRouteData(context.Object);

            Assert.NotNull(route);
            Assert.Equal("StagingApi", route.Values["controller"]);
            Assert.Equal(action, route.Values["action"]);
        }

        [Fact]
        public async Task DownloadsAuthorizedPackageWithCanonicalFilename()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("owner") { Key = 2 };
            var stagedPackage = CreateStagedPackage(owner);
            using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "packagea", "1.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(stagedPackage))
                .ReturnsAsync(content);

            var result = await target.DownloadStagedPackage("packagea", "1.0");

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(CoreConstants.OctetStreamContentType, file.ContentType);
            Assert.Equal("PackageA.1.0.0.nupkg", file.FileDownloadName);
        }

        [Theory]
        [InlineData("GET", "", nameof(StagingApiController.DownloadStagedSymbolPackage))]
        [InlineData("GET", "/status", nameof(StagingApiController.GetStagedSymbolPackageStatus))]
        [InlineData("DELETE", "", nameof(StagingApiController.DeleteStagedSymbolPackage))]
        public void MapsSymbolContentStatusAndDeletionRoutes(string method, string suffix, string action)
        {
            var routes = new RouteCollection();
            Routes.RegisterStagingApiRoutes(routes);
            var context = new Mock<HttpContextBase>();
            context.SetupGet(x => x.Request.AppRelativeCurrentExecutionFilePath).Returns($"~/api/v3/staging/symbols/PackageA/1.0.0{suffix}");
            context.SetupGet(x => x.Request.PathInfo).Returns(string.Empty);
            context.SetupGet(x => x.Request.HttpMethod).Returns(method);

            var route = routes.GetRouteData(context.Object);

            Assert.NotNull(route);
            Assert.Equal("StagingApi", route.Values["controller"]);
            Assert.Equal(action, route.Values["action"]);
        }

        [Fact]
        public async Task DownloadsAuthorizedSymbolsWithCanonicalFilename()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("owner") { Key = 2 };
            var symbol = new StagedSymbolPackage { StagedPackageIdentity = CreateStagedPackage(owner).StagedPackageIdentity };
            using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedSymbolPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "packagea", "1.0"))
                .Returns(symbol);
            GetMock<ISymbolPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(symbol))
                .ReturnsAsync(content);

            var result = await target.DownloadStagedSymbolPackage("packagea", "1.0");

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(CoreConstants.OctetStreamContentType, file.ContentType);
            Assert.Equal("PackageA.1.0.0.snupkg", file.FileDownloadName);
        }

        [Fact]
        public async Task RejectsSymbolDownloadWithoutEnabledApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = await target.DownloadStagedSymbolPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedSymbolPackage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.OpenPackageContentAsync(It.IsAny<StagedSymbolPackage>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReturnsNotFoundForUnavailableSymbolDownload(bool hasAttempt)
        {
            var owner = new User("owner") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, owner, owner);
            if (hasAttempt)
            {
                var symbol = new StagedSymbolPackage { StagedPackageIdentity = CreateStagedPackage(owner).StagedPackageIdentity };
                GetMock<IPackageStagingManagementService>()
                    .Setup(x => x.GetStagedSymbolPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "PackageA", "1.0.0"))
                    .Returns(symbol);
                GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.OpenPackageContentAsync(symbol)).ReturnsAsync((Stream)null);
            }

            var result = await target.DownloadStagedSymbolPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.NotFound, "SymbolPackageNotFound");
            GetMock<ISymbolPackageStagingManagementService>().Verify(
                x => x.OpenPackageContentAsync(It.IsAny<StagedSymbolPackage>()), hasAttempt ? Times.Once() : Times.Never());
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void GetsArtifactResourceMatchingInventory(bool symbols, bool grouped)
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new Organization("owner") { Key = 2 };
            var package = CreateStagedPackage(owner);
            package.Status = StagedPackageStatus.Ready;
            package.UploadedDate = DateTime.UtcNow;
            if (grouped)
            {
                var group = CreateStagingGroup(10, "release", "Release", owner, package.UploadedDate);
                package.StagedPackageIdentity.StagingGroupKey = group.Key;
                package.StagedPackageIdentity.StagingGroup = group;
            }

            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            StagingArtifactResponse expected;
            if (symbols)
            {
                package.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                var symbol = new StagedSymbolPackage
                {
                    Key = 44,
                    StagedPackageIdentity = package.StagedPackageIdentity,
                    SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged },
                    Status = StagedPackageStatus.Ready,
                    UploadedDate = package.UploadedDate,
                };
                GetMock<IPackageStagingManagementService>()
                    .Setup(x => x.GetStagedSymbolPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "packagea", "1.0"))
                    .Returns(symbol);
                expected = StagingArtifactResponse.FromSymbolPackage(
                    symbol, StagingExpirationPolicy.GetDeadline(symbol), target.Url.ManageUngroupedStaging(owner.Username, relativeUrl: false));
            }
            else
            {
                GetMock<IPackageStagingManagementService>()
                    .Setup(x => x.GetStagedPackage(owner, It.IsAny<IReadOnlyCollection<Scope>>(), "packagea", "1.0"))
                    .Returns(package);
                expected = StagingArtifactResponse.FromPackage(
                    package, StagingExpirationPolicy.GetDeadline(package), target.Url.ManageStagingGroup(owner.Username, "release", relativeUrl: false));
            }

            var result = symbols ? target.GetStagedSymbolPackageStatus("packagea", "1.0") : target.GetStagedPackageStatus("packagea", "1.0");

            ParseJsonContent(result);
            Assert.Equal(JsonConvert.SerializeObject(expected), Assert.IsType<ContentResult>(result).Content);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RejectsArtifactReadWithoutEnabledApiKeyOwner(bool symbols)
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = symbols ? target.GetStagedSymbolPackageStatus("PackageA", "1.0.0") : target.GetStagedPackageStatus("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedPackage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedSymbolPackage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsStructuredNotFoundForUnavailableArtifact(bool symbols)
        {
            var owner = new User("owner") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, owner, owner);

            var result = symbols ? target.GetStagedSymbolPackageStatus("PackageA", "1.0.0") : target.GetStagedPackageStatus("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.NotFound, symbols ? "SymbolPackageNotFound" : "PackageNotFound");
        }

        [Fact]
        public async Task HidesUnavailablePackageDownloadWithoutOpeningContent()
        {
            var owner = new User("owner") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, owner, owner);

            var result = await target.DownloadStagedPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.NotFound, "PackageNotFound");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.OpenPackageContentAsync(It.IsAny<StagedPackage>()),
                Times.Never);
        }

        [Fact]
        public async Task RejectsPackageDownloadWithoutEnabledApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner: null);

            var result = await target.DownloadStagedPackage("PackageA", "1.0.0");

            AssertError(target, result, HttpStatusCode.Forbidden, "StagingOwnerUnavailable");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedPackage(It.IsAny<User>(), It.IsAny<IReadOnlyCollection<Scope>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            GetMock<IPackageStagingManagementService>().Verify(x => x.OpenPackageContentAsync(It.IsAny<StagedPackage>()), Times.Never);
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
                .ReturnsAsync(true);
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
        public async Task ReportsConflictWhenPromotionPreventsListedUpdate()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(currentUser, It.IsAny<IEnumerable<Scope>>(), stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.UpdateListedAsync(stagedPackage, true))
                .ReturnsAsync(false);
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
            Assert.Equal(409, status.StatusCode);
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
                .ReturnsAsync(true);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeleteStagedPackage("PackageA", "1.0.0");

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(204, status.StatusCode);
        }

        [Fact]
        public async Task ReportsConflictWhenPackageDeletionIsRejected()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            stagedPackage.StagedPackageIdentity.CurrentStagedSymbolPackageKey = 100;

            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(currentUser, It.IsAny<IEnumerable<Scope>>(), stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeletePackageAsync(stagedPackage))
                .ReturnsAsync(false);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeleteStagedPackage("PackageA", "1.0.0");

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(409, status.StatusCode);
        }

        [Fact]
        public async Task DeletesAnOwnerVisibleStagingGroup()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var group = CreateStagingGroup(10, "release", "Release", owner, new DateTime(2026, 9, 1));
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(owner, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeleteStagingGroupAsync(owner, group))
                .ReturnsAsync(StagingGroupDeletionResult.Deleted(2));

            var result = await target.DeleteStagingGroup(group.Id);

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(204, status.StatusCode);
        }

        [Fact]
        public async Task RejectsDeletingAStagingGroupWhilePromotionIsActive()
        {
            var currentUser = new User("current") { Key = 1 };
            var owner = new User("example-org") { Key = 2 };
            var group = CreateStagingGroup(10, "release", "Release", owner, new DateTime(2026, 9, 1));
            var target = GetController<StagingApiController>();
            ConfigureCreateGroupRequest(target, currentUser, owner);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(owner, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeleteStagingGroupAsync(owner, group))
                .ReturnsAsync(StagingGroupDeletionResult.Conflict(1));

            var result = await target.DeleteStagingGroup(group.Id);

            AssertError(target, result, HttpStatusCode.Conflict, "GroupPromotionInProgress");
        }

        private void ConfigureCreateGroupRequest(StagingApiController target, User currentUser, User owner)
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
                        new Scope(owner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackageStage)
                        {
                            OwnerKey = owner.Key,
                        },
                    });
                GetMock<IPackageStagingAuthorizationService>()
                    .Setup(x => x.GetEnabledApiKeyOwner(currentUser, It.IsAny<IEnumerable<Scope>>()))
                    .Returns(owner);
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
            Assert.Equal(JsonRequestBehavior.AllowGet, json.JsonRequestBehavior);
            Mock.Get(controller.Response).VerifySet(x => x.StatusCode = (int)statusCode);
            var body = JObject.FromObject(json.Data);
            Assert.Equal(code, (string)body["error"]["code"]);
            if (target != null)
            {
                Assert.Equal(target, (string)body["error"]["target"]);
            }
        }

        private static JObject ParseJsonContent(ActionResult result)
        {
            var content = Assert.IsType<ContentResult>(result);
            Assert.Equal("application/json", content.ContentType);
            using (var reader = new JsonTextReader(new StringReader(content.Content)) { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }

        private static StagingGroup CreateStagingGroup(int key, string id, string name, User owner, DateTime createdDate)
        {
            return new StagingGroup
            {
                Key = key,
                Id = id,
                Name = name,
                Owner = owner,
                OwnerKey = owner.Key,
                CreatedDate = createdDate,
            };
        }

        private static StagedPackage CreateStagedPackage(User owner)
        {
            var package = new Package
            {
                Key = 42,
                NormalizedVersion = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "PackageA" },
            };
            package.PackageRegistration.Owners.Add(owner);
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
