// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class StagingControllerFacts : TestContainer
    {
        [Fact]
        public void DisplaysCreateGroupFormForEnabledOwners()
        {
            var currentUser = new User("current") { Key = 1 };
            var organization = new Organization("organization") { Key = 2 };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwners(currentUser))
                .Returns(new User[] { organization, currentUser });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var model = ResultAssert.IsView<CreateStagingGroupViewModel>(target.CreateGroup());

            Assert.Equal(currentUser.Username, model.Owner);
            Assert.Equal(new[] { organization.Username, currentUser.Username }, model.Owners);
        }

        [Fact]
        public async Task CreatesAGroupAndRedirectsToTheExpandedGroupList()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Owner = currentUser,
                OwnerKey = currentUser.Key,
                Id = "release.1",
                Name = "Release 1",
            };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwners(currentUser))
                .Returns(new[] { currentUser });
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupAsync(currentUser, group.Id, group.Name))
                .ReturnsAsync(CreateStagingGroupResult.Created(group));
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);
            var model = new CreateStagingGroupViewModel
            {
                Owner = currentUser.Username,
                Id = group.Id,
                Name = group.Name,
            };

            var result = await target.CreateGroup(model);

            ResultAssert.IsRedirectTo(result, "/account/Packages#show-staging-groups-container");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.CreateStagingGroupAsync(currentUser, group.Id, group.Name),
                Times.Once);
        }

        [Fact]
        public async Task RejectsDuplicateGroupId()
        {
            var currentUser = new User("current") { Key = 1 };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwners(currentUser))
                .Returns(new[] { currentUser });
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.CreateStagingGroupAsync(currentUser, "release.1", "Release 1"))
                .ReturnsAsync(CreateStagingGroupResult.GroupAlreadyExists());
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);
            var model = new CreateStagingGroupViewModel
            {
                Owner = currentUser.Username,
                Id = "release.1",
                Name = "Release 1",
            };

            var result = await target.CreateGroup(model);

            var returnedModel = ResultAssert.IsView<CreateStagingGroupViewModel>(result);
            Assert.Same(model, returnedModel);
            Assert.Equal(new[] { currentUser.Username }, returnedModel.Owners);
            Assert.Contains(
                target.ModelState[nameof(model.Id)].Errors,
                error => error.ErrorMessage == "A staging group with this ID already exists.");
        }

        [Fact]
        public async Task RedisplaysInvalidCreateGroupForm()
        {
            var currentUser = new User("current") { Key = 1 };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwners(currentUser))
                .Returns(new[] { currentUser });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);
            target.ModelState.AddModelError("Id", "The Group ID field is required.");
            var model = new CreateStagingGroupViewModel
            {
                Owner = currentUser.Username,
            };

            var result = await target.CreateGroup(model);

            Assert.Same(model, ResultAssert.IsView<CreateStagingGroupViewModel>(result));
            Assert.Equal(new[] { currentUser.Username }, model.Owners);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.CreateStagingGroupAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HidesCreateGroupForAnUnavailableOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwners(currentUser))
                .Returns(new[] { currentUser });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.CreateGroup(new CreateStagingGroupViewModel
            {
                Owner = "other",
                Id = "release.1",
                Name = "Release 1",
            });

            Assert.IsType<HttpNotFoundResult>(result);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.CreateStagingGroupAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void DisplaysAnOwnerVisibleGroupAndItsCurrentMembers()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "test-group",
                Name = "Test group",
                CreatedDate = new System.DateTime(2026, 9, 10),
            };
            var failedPackage = CreateStagedPackage(42, "Failed.Package", "1.0.0", currentUser, StagedPackageStatus.FailedValidation);
            failedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
            var readyPackage = CreateStagedPackage(43, "Ready.Package", "2.0.0", currentUser, StagedPackageStatus.Ready);
            readyPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
            var ungroupedPackage = CreateStagedPackage(44, "Ungrouped.Package", "3.0.0", currentUser, StagedPackageStatus.Ready);
            var validationIssue = ValidationIssue.PackageIsZip64;
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, "current"))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, "test-group"))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(new[] { readyPackage, ungroupedPackage, failedPackage });
            GetMock<IValidationService>()
                .Setup(x => x.GetStagedPackageValidationIssues(
                    It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { failedPackage.Key }))))
                .Returns(new Dictionary<int, IReadOnlyList<ValidationIssue>>
                {
                    { failedPackage.Key, new[] { validationIssue } },
                });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Group("current", "test-group");

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result);
            Assert.Equal("Test group", model.Name);
            Assert.Equal(2, model.PackageCount);
            Assert.Equal(1, model.ReadyCount);
            Assert.Equal(1, model.FailedCount);
            Assert.Equal(new[] { "Failed.Package", "Ready.Package" }, model.Packages.Select(package => package.Id));
            Assert.Equal(new[] { validationIssue }, model.Packages.First().ValidationIssues);
            Assert.All(model.Packages, package => Assert.True(package.CanManage));
            Assert.False(model.Packages.First().CanPromote);
            Assert.True(model.Packages.Last().CanPromote);
        }

        [Fact]
        public void DisplaysUngroupedPackagesForAnOwner()
        {
            var currentUser = new User("Current") { Key = 1 };
            var ungroupedPackage = CreateStagedPackage(42, "Ungrouped.Package", "1.0.0", currentUser, StagedPackageStatus.Ready);
            var groupedPackage = CreateStagedPackage(43, "Grouped.Package", "2.0.0", currentUser, StagedPackageStatus.Ready);
            groupedPackage.StagedPackageIdentity.StagingGroupKey = 10;
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(new[] { groupedPackage, ungroupedPackage });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Ungrouped("current");

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.Equal(currentUser.Username, model.Owner);
            Assert.True(model.IsUngrouped);
            Assert.Null(model.Id);
            Assert.Equal("Ungrouped", model.Name);
            Assert.Equal("Staged packages not in any group", model.Description);
            Assert.Equal("Ungrouped.Package", Assert.Single(model.Packages).Id);
        }

        [Fact]
        public void HidesEmptyOrUnauthorizedUngroupedPackages()
        {
            var currentUser = new User("current") { Key = 1 };
            var otherOwner = new User("other") { Key = 2 };
            var otherPackage = CreateStagedPackage(42, "Other.Package", "1.0.0", otherOwner, StagedPackageStatus.Ready);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(new[] { otherPackage });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Ungrouped(currentUser.Username);

            Assert.IsType<HttpNotFoundResult>(result);
        }

        [Fact]
        public void ListsAllGroupMembersDeterministically()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "test-group",
                Name = "Test group",
            };
            var stagedPackages = Enumerable
                .Range(1, GalleryConstants.DefaultPackageListPageSize + 1)
                .Select(index =>
                {
                    var package = CreateStagedPackage(index, $"Package.{index:D2}", "1.0.0", currentUser, StagedPackageStatus.Ready);
                    package.StagedPackageIdentity.StagingGroupKey = group.Key;
                    return package;
                })
                .Reverse()
                .ToList();
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(stagedPackages);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Group(currentUser.Username, group.Id);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result);
            Assert.Equal(
                Enumerable.Range(1, GalleryConstants.DefaultPackageListPageSize + 1).Select(index => $"Package.{index:D2}"),
                model.Packages.Select(package => package.Id));
        }

        [Fact]
        public void HidesMissingOrUnauthorizedGroups()
        {
            var currentUser = new User("current") { Key = 1 };
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Group("other", "test-group");

            Assert.IsType<HttpNotFoundResult>(result);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.GetStagedPackages(It.IsAny<User>()),
                Times.Never);
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
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(stagedPackage))
                .ReturnsAsync(content);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadPackage("PackageA", "1.0.0");

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(CoreConstants.PackageContentType, file.ContentType);
            Assert.Equal("PackageA.1.0.0.nupkg", file.FileDownloadName);
        }

        [Fact]
        public async Task HidesUnauthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(false);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadPackage("PackageA", "1.0.0");

            Assert.IsType<HttpNotFoundResult>(result);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.OpenPackageContentAsync(It.IsAny<StagedPackage>()),
                Times.Never);
        }

        [Fact]
        public async Task HidesPackageWhenContentIsMissing()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(stagedPackage))
                .ReturnsAsync((Stream)null);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadPackage("PackageA", "1.0.0");

            Assert.IsType<HttpNotFoundResult>(result);
        }

        [Fact]
        public async Task UpdatesListedIntentForAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.UpdateListedAsync(stagedPackage, true))
                .Returns(Task.CompletedTask);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.UpdateListed("PackageA", "1.0.0", listed: true);

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(HttpStatusCode.NoContent, (HttpStatusCode)status.StatusCode);
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
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeletePackageAsync(stagedPackage))
                .Returns(Task.CompletedTask);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeletePackage("PackageA", "1.0.0");

            Assert.IsType<RedirectResult>(result);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.DeletePackageAsync(stagedPackage),
                Times.Once);
        }

        [Fact]
        public async Task PromotesAuthorizedReadyPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingPromotionService>()
                .Setup(x => x.PromotePackageAsync(currentUser, stagedPackage))
                .ReturnsAsync(PackageStagingPromotionResult.Accepted);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.PromotePackage("PackageA", "1.0.0");

            Assert.IsType<RedirectResult>(result);
            GetMock<IPackageStagingPromotionService>().Verify(
                x => x.PromotePackageAsync(currentUser, stagedPackage),
                Times.Once);
        }

        [Fact]
        public async Task ReportsWhenPackageIsNotReadyForPromotion()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingPromotionService>()
                .Setup(x => x.PromotePackageAsync(currentUser, stagedPackage))
                .ReturnsAsync(PackageStagingPromotionResult.NotReady);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.PromotePackage("PackageA", "1.0.0");

            Assert.IsType<RedirectResult>(result);
            Assert.Equal("The staged package is not ready for promotion.", target.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task ReplacesAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            var packageFile = new Mock<HttpPostedFileBase>();
            using var content = new MemoryStream();
            packageFile.SetupGet(x => x.ContentLength).Returns(1);
            packageFile.SetupGet(x => x.InputStream).Returns(content);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingUploadService>()
                .Setup(x => x.ReplacePackageAsync(currentUser, It.IsAny<HttpContextBase>(), stagedPackage, content))
                .ReturnsAsync(PackageStagingResult.Ok());
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.ReplacePackage("PackageA", "1.0.0", packageFile.Object);

            Assert.IsType<RedirectResult>(result);
            GetMock<IPackageStagingUploadService>().Verify(
                x => x.ReplacePackageAsync(currentUser, It.IsAny<HttpContextBase>(), stagedPackage, content),
                Times.Once);
        }

        [Fact]
        public async Task RequiresAReplacementFile()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.ReplacePackage("PackageA", "1.0.0", packageFile: null);

            Assert.IsType<RedirectResult>(result);
            Assert.Equal("Select a package file.", target.TempData["ErrorMessage"]);
            GetMock<IPackageStagingUploadService>().Verify(
                x => x.ReplacePackageAsync(
                    It.IsAny<User>(),
                    It.IsAny<HttpContextBase>(),
                    It.IsAny<StagedPackage>(),
                    It.IsAny<Stream>()),
                Times.Never);
        }

        private static StagedPackage CreateStagedPackage(User owner)
        {
            return CreateStagedPackage(43, "PackageA", "1.0.0", owner, StagedPackageStatus.Validating);
        }

        private static StagedPackage CreateStagedPackage(
            int key,
            string id,
            string version,
            User owner,
            StagedPackageStatus status)
        {
            var package = new Package
            {
                Key = key,
                NormalizedVersion = version,
                PackageRegistration = new PackageRegistration { Id = id },
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
                Key = key,
                StagedPackageIdentityKey = identity.Key,
                StagedPackageIdentity = identity,
                Status = status,
            };
            identity.CurrentStagedPackageKey = stagedPackage.Key;
            identity.CurrentStagedPackage = stagedPackage;
            return stagedPackage;
        }
    }
}
