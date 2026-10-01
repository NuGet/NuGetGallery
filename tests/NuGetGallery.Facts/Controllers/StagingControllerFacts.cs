// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public StagingControllerFacts()
        {
            GetMock<ISymbolPackageStagingManagementService>()
                .Setup(x => x.GetStagedSymbolPackages(It.IsAny<User>()))
                .Returns(new List<StagedSymbolPackage>());
        }

        [Theory]
        [InlineData(PackageStatus.Available)]
        [InlineData(PackageStatus.Deleted)]
        public void DisplaysUngroupedSymbolFindingsAndParentStatus(PackageStatus parentStatus)
        {
            var owner = new User("owner") { Key = 1 };
            var attempt = CreateStagedSymbolPackage(owner);
            attempt.Status = StagedPackageStatus.FailedValidation;
            attempt.StagedPackageIdentity.Package.PackageStatusKey = parentStatus;
            var grouped = CreateStagedSymbolPackage(owner);
            grouped.StagedPackageIdentity.StagingGroupKey = 10;
            var otherOwner = CreateStagedSymbolPackage(new User("other") { Key = 2 });
            var issue = ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound;
            GetMock<IPackageStagingAuthorizationService>().Setup(x => x.GetEnabledOwner(owner, owner.Username)).Returns(owner);
            GetMock<IPackageStagingManagementService>().Setup(x => x.GetStagedPackages(owner)).Returns(new List<StagedPackage>());
            GetMock<IPackageStagingManagementService>().Setup(x => x.GetStagingGroups(owner)).Returns(new List<StagingGroup>());
            GetMock<ISymbolPackageStagingManagementService>()
                .Setup(x => x.GetStagedSymbolPackages(owner))
                .Returns(new[] { attempt, grouped, otherOwner });
            GetMock<IValidationService>()
                .Setup(x => x.GetStagedSymbolPackageValidationIssues(It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { attempt.Key }))))
                .Returns(new Dictionary<int, IReadOnlyList<ValidationIssue>> { { attempt.Key, new[] { issue } } });
            var target = GetController<StagingController>();
            target.SetCurrentUser(owner);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(target.Ungrouped(owner.Username), viewName: "Group");

            var symbols = Assert.Single(model.Packages);
            Assert.True(symbols.IsSymbolPackage);
            Assert.Equal(parentStatus.ToString(), symbols.ParentStatus);
            Assert.Equal(parentStatus == PackageStatus.Available, symbols.ParentUrl != null);
            Assert.Same(issue, Assert.Single(symbols.ValidationIssues));
            Assert.True(symbols.CanManage);
            Assert.False(symbols.CanPromote);
            Assert.False(symbols.CanResend);
            Assert.Null(symbols.MoveUrl);
            Assert.Equal(1, model.PackageCount);
            Assert.Equal(1, model.FailedCount);
            GetMock<IValidationService>().Verify(x => x.GetStagedPackageValidationIssues(It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
        }

        [Theory]
        [InlineData(StagedPackageStatus.Validating)]
        [InlineData(StagedPackageStatus.Ready)]
        [InlineData(StagedPackageStatus.FailedValidation)]
        [InlineData(StagedPackageStatus.Ready, true)]
        [InlineData(StagedPackageStatus.FailedValidation, true)]
        public void DisplaysPrivateStagedParentStatusAndLink(StagedPackageStatus parentStatus, bool grouped = false)
        {
            var owner = new User("owner") { Key = 1 };
            var parent = CreateStagedPackage(owner);
            parent.Status = parentStatus;
            parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Staged;
            var group = new StagingGroup { Key = 10, Owner = owner, OwnerKey = owner.Key, Id = "release", Name = "Release" };
            if (grouped)
            {
                parent.StagedPackageIdentity.StagingGroup = group;
                parent.StagedPackageIdentity.StagingGroupKey = group.Key;
            }

            var attempt = new StagedSymbolPackage { Key = 100, StagedPackageIdentity = parent.StagedPackageIdentity, Status = StagedPackageStatus.Validating };
            GetMock<IPackageStagingAuthorizationService>().Setup(x => x.GetEnabledOwner(owner, owner.Username)).Returns(owner);
            GetMock<IPackageStagingManagementService>().Setup(x => x.GetStagedPackages(owner)).Returns(new[] { parent });
            GetMock<IPackageStagingManagementService>().Setup(x => x.GetStagingGroups(owner)).Returns(new[] { group });
            GetMock<IPackageStagingManagementService>().Setup(x => x.FindStagingGroup(owner, group.Id)).Returns(group);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.GetStagedSymbolPackages(owner)).Returns(new[] { attempt });
            GetMock<IValidationService>().Setup(x => x.GetStagedPackageValidationIssues(It.IsAny<IReadOnlyCollection<int>>())).Returns(new Dictionary<int, IReadOnlyList<ValidationIssue>>());
            var target = GetController<StagingController>();
            target.SetCurrentUser(owner);

            var result = grouped ? target.Group(owner.Username, group.Id) : target.Ungrouped(owner.Username);
            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");

            Assert.Equal(2, model.PackageCount);
            var symbols = Assert.Single(model.Packages.Where(package => package.IsSymbolPackage));
            Assert.Equal(parentStatus.ToString(), symbols.ParentStatus);
            Assert.Equal("/account/staging/package/PackageA/1.0.0/content", symbols.ParentUrl);
            Assert.NotNull(symbols.MoveUrl);
            Assert.False(model.CanPromote);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(true, true, true)]
        public async Task SymbolActionsHonorAuthorizationAndDeletionOutcome(bool authorized, bool deletionSucceeds, bool grouped = false)
        {
            var owner = new User("owner") { Key = 1 };
            var attempt = CreateStagedSymbolPackage(owner);
            if (grouped)
            {
                attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { Key = 10, Id = "release" };
                attempt.StagedPackageIdentity.StagingGroupKey = 10;
            }

            using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            GetMock<ISymbolPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedSymbolPackage("PackageA", "1.0.0"))
                .Returns(attempt);
            GetMock<IPackageStagingAuthorizationService>().Setup(x => x.CanManage(owner, attempt)).Returns(authorized);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.OpenPackageContentAsync(attempt)).ReturnsAsync(content);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.DeletePackageAsync(attempt)).ReturnsAsync(deletionSucceeds);
            var target = GetController<StagingController>();
            target.SetCurrentUser(owner);

            var download = await target.DownloadSymbolPackage("PackageA", "1.0.0");
            var delete = await target.DeleteSymbolPackage("PackageA", "1.0.0");

            if (authorized)
            {
                var file = Assert.IsType<FileStreamResult>(download);
                Assert.Same(content, file.FileStream);
                Assert.Equal("PackageA.1.0.0.snupkg", file.FileDownloadName);
                Assert.Equal(CoreConstants.OctetStreamContentType, file.ContentType);
                ResultAssert.IsRedirectTo(delete, grouped ? "/account/staging/owner/groups/release" : "/account/staging/owner/ungrouped");
                GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.DeletePackageAsync(attempt), Times.Once);
                if (!deletionSucceeds)
                {
                    Assert.Equal("The staged symbols changed or promotion started. Refresh and try again.", target.TempData["ErrorMessage"]);
                }
            }
            else
            {
                Assert.IsType<HttpNotFoundResult>(download);
                Assert.IsType<HttpNotFoundResult>(delete);
                GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.OpenPackageContentAsync(It.IsAny<StagedSymbolPackage>()), Times.Never);
                GetMock<ISymbolPackageStagingManagementService>().Verify(x => x.DeletePackageAsync(It.IsAny<StagedSymbolPackage>()), Times.Never);
            }
        }

        [Theory]
        [InlineData(false, true, false)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        public async Task SymbolReplacementHonorsAuthorizationAndReturnsToItsGroup(bool authorized, bool succeeds, bool grouped)
        {
            var owner = new User("owner") { Key = 1 };
            var attempt = CreateStagedSymbolPackage(owner);
            if (grouped)
            {
                attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { Key = 10, Id = "release" };
                attempt.StagedPackageIdentity.StagingGroupKey = 10;
            }

            using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var file = new Mock<HttpPostedFileBase>();
            file.Setup(x => x.ContentLength).Returns(3);
            file.Setup(x => x.InputStream).Returns(content);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.FindCurrentStagedSymbolPackage("PackageA", "1.0.0")).Returns(attempt);
            GetMock<IPackageStagingAuthorizationService>().Setup(x => x.CanManage(owner, attempt)).Returns(authorized);
            var uploadResult = succeeds ? PackageStagingResult.Ok() : PackageStagingResult.Error(HttpStatusCode.Conflict, "Replacement failed.");
            GetMock<ISymbolPackageStagingUploadService>()
                .Setup(x => x.ReplaceSymbolPackageAsync(owner, It.IsAny<HttpContextBase>(), attempt, content))
                .ReturnsAsync(uploadResult);
            var target = GetController<StagingController>();
            target.SetCurrentUser(owner);

            var result = await target.ReplaceSymbolPackage("PackageA", "1.0.0", file.Object);

            if (authorized)
            {
                ResultAssert.IsRedirectTo(result, grouped ? "/account/staging/owner/groups/release" : "/account/staging/owner/ungrouped");
                Assert.Equal(succeeds ? null : "Replacement failed.", target.TempData["ErrorMessage"]);
                GetMock<ISymbolPackageStagingUploadService>().Verify(x => x.ReplaceSymbolPackageAsync(owner, It.IsAny<HttpContextBase>(), attempt, content), Times.Once);
            }
            else
            {
                Assert.IsType<HttpNotFoundResult>(result);
                GetMock<ISymbolPackageStagingUploadService>().Verify(x => x.ReplaceSymbolPackageAsync(It.IsAny<User>(), It.IsAny<HttpContextBase>(), It.IsAny<StagedSymbolPackage>(), It.IsAny<Stream>()), Times.Never);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MovesSymbolOnlyIdentityUsingOwnerAuthorization(bool authorized)
        {
            var owner = new User("owner") { Key = 1 };
            var symbols = CreateStagedSymbolPackage(owner);
            var group = new StagingGroup { Key = 10, Owner = owner, OwnerKey = owner.Key, Id = "release", Name = "Release" };
            GetMock<IPackageStagingAuthorizationService>().Setup(x => x.GetEnabledOwner(owner, owner.Username)).Returns(owner);
            GetMock<IPackageStagingAuthorizationService>().Setup(x => x.CanManage(owner, symbols)).Returns(authorized);
            GetMock<ISymbolPackageStagingManagementService>().Setup(x => x.FindCurrentStagedSymbolPackage("PackageA", "1.0.0")).Returns(symbols);
            GetMock<IPackageStagingManagementService>().Setup(x => x.FindStagingGroup(owner, group.Id)).Returns(group);
            GetMock<IPackageStagingManagementService>().Setup(x => x.MovePackageIdentityAsync(owner, symbols.StagedPackageIdentity, group)).ReturnsAsync(StagingGroupMembershipResult.Updated);
            var target = GetController<StagingController>();
            target.SetCurrentUser(owner);

            var result = await target.MovePackage(owner.Username, "PackageA", "1.0.0", group.Id);

            if (authorized)
            {
                ResultAssert.IsRedirectTo(result, "/account/staging/owner/groups/release");
            }
            else
            {
                Assert.IsType<HttpNotFoundResult>(result);
                GetMock<IPackageStagingManagementService>().Verify(x => x.MovePackageIdentityAsync(It.IsAny<User>(), It.IsAny<StagedPackageIdentity>(), It.IsAny<StagingGroup>()), Times.Never);
            }
        }

        private static StagedSymbolPackage CreateStagedSymbolPackage(User owner)
        {
            var identity = CreateStagedPackage(owner).StagedPackageIdentity;
            identity.CurrentStagedPackageKey = null;
            identity.CurrentStagedPackage = null;
            identity.Package.PackageStatusKey = PackageStatus.Available;
            var attempt = new StagedSymbolPackage
            {
                Key = 100,
                StagedPackageIdentity = identity,
                Status = StagedPackageStatus.Ready,
            };
            identity.CurrentStagedSymbolPackageKey = attempt.Key;
            identity.CurrentStagedSymbolPackage = attempt;
            return attempt;
        }

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

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("-release")]
        [InlineData("release_")]
        public void RejectsGroupIdsThatDoNotStartAndEndWithLettersOrNumbers(string id)
        {
            var model = new CreateStagingGroupViewModel
            {
                Owner = "current",
                Id = id,
                Name = "Release",
            };
            var validationResults = new List<ValidationResult>();

            var isValid = Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true);

            Assert.False(isValid);
            Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(model.Id)));
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
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
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

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.Equal("Test group", model.Name);
            Assert.Equal(2, model.PackageCount);
            Assert.Equal(1, model.ReadyCount);
            Assert.Equal(1, model.FailedCount);
            Assert.False(model.CanPromote);
            Assert.Equal(new[] { "Failed.Package", "Ready.Package" }, model.Packages.Select(package => package.Id));
            Assert.Equal(new[] { validationIssue }, model.Packages.First().ValidationIssues);
            Assert.All(model.Packages, package => Assert.True(package.CanManage));
            Assert.False(model.Packages.First().CanPromote);
            Assert.False(model.Packages.Last().CanPromote);
            Assert.All(model.Packages, package =>
            {
                Assert.EndsWith(
                    $"/account/staging/current/package/{package.Id}/{package.Version}/move",
                    package.MoveUrl);
            });
        }

        [Fact]
        public void DisplaysRemainingPackagesAndFreezesAnActiveGroup()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "test-group",
                Name = "Test group",
                ActivePromotionId = System.Guid.NewGuid(),
                PromotionMessageSentDate = System.DateTime.UtcNow.AddHours(-2),
            };
            var promotingPackage = CreateStagedPackage(42, "Promoting.Package", "1.0.0", currentUser, StagedPackageStatus.Promoting);
            promotingPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
            var failedPackage = CreateStagedPackage(43, "Failed.Package", "1.0.0", currentUser, StagedPackageStatus.PromotionFailed);
            failedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(new[] { promotingPackage, failedPackage });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Group(currentUser.Username, group.Id);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.True(model.IsPromotionActive);
            Assert.True(model.CanResend);
            Assert.False(model.CanPromote);
            Assert.Equal(1, model.PromotingCount);
            Assert.Equal(1, model.PromotionFailedCount);
            Assert.Equal(2, model.PackageCount);
            Assert.All(model.Packages, package =>
            {
                Assert.False(package.CanManage);
                Assert.Null(package.MoveUrl);
            });
        }

        [Fact]
        public void AllowsPromotionWhenEveryPackageInAnInactiveGroupIsReady()
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
            var readyPackages = new[]
            {
                CreateStagedPackage(42, "First.Package", "1.0.0", currentUser, StagedPackageStatus.Ready),
                CreateStagedPackage(43, "Second.Package", "2.0.0", currentUser, StagedPackageStatus.Ready),
                CreateStagedPackage(44, "Third.Package", "3.0.0", currentUser, StagedPackageStatus.Ready),
            };
            foreach (var package in readyPackages)
            {
                package.StagedPackageIdentity.StagingGroupKey = group.Key;
            }
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(readyPackages);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Group(currentUser.Username, group.Id);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.True(model.CanPromote);
            Assert.False(model.IsPromotionActive);
            Assert.Equal(3, model.PackageCount);
            Assert.Equal(3, model.ReadyCount);
        }

        [Fact]
        public async Task RenamesAnOwnerVisibleGroup()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.RenameStagingGroupAsync(currentUser, group.Id, "Release 2"))
                .ReturnsAsync(() =>
                {
                    group.Name = "Release 2";
                    return group;
                });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.RenameGroup(
                currentUser.Username,
                group.Id,
                new RenameStagingGroupViewModel { Name = " Release 2 " });

            ResultAssert.IsRedirectTo(result, "/account/staging/current/groups/release");
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.RenameStagingGroupAsync(currentUser, group.Id, "Release 2"),
                Times.Once);
        }

        [Fact]
        public async Task RedisplaysTheGroupForAnInvalidRename()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(Enumerable.Empty<StagedPackage>().ToList());
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);
            target.ModelState.AddModelError("Name", "The Display name field is required.");

            var result = await target.RenameGroup(
                currentUser.Username,
                group.Id,
                new RenameStagingGroupViewModel { Name = " " });

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.Equal(group.Name, model.Name);
            Assert.True(target.ModelState.ContainsKey("Name"));
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.RenameStagingGroupAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void DisplaysDeleteGroupConfirmationWithCurrentPackageCount()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            var packages = new[]
            {
                CreateStagedPackage(42, "First.Package", "1.0.0", currentUser, StagedPackageStatus.Ready),
                CreateStagedPackage(43, "Second.Package", "2.0.0", currentUser, StagedPackageStatus.FailedValidation),
            };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupSummaries(currentUser))
                .Returns(new[] { new StagingGroupSummary(group, packages) });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.DeleteGroup(currentUser.Username, group.Id);

            var model = ResultAssert.IsView<DeleteStagingGroupViewModel>(result);
            Assert.Equal(group.Name, model.Name);
            Assert.Equal(group.Id, model.Id);
            Assert.Equal(2, model.PackageCount);
        }

        [Fact]
        public async Task DeletesAGroupAndRedirectsToManagePackages()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupSummaries(currentUser))
                .Returns(new[] { new StagingGroupSummary(group, Enumerable.Empty<StagedPackage>().ToList()) });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeleteStagingGroupAsync(currentUser, group))
                .ReturnsAsync(StagingGroupDeletionResult.Deleted(0));
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeleteGroupPost(currentUser.Username, group.Id);

            ResultAssert.IsRedirectTo(result, "/account/Packages");
        }

        [Fact]
        public async Task RedisplaysDeleteGroupConfirmationWhenPromotionIsActive()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            var package = CreateStagedPackage(42, "Test.Package", "1.0.0", currentUser, StagedPackageStatus.Promoting);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroupSummaries(currentUser))
                .Returns(new[] { new StagingGroupSummary(group, new[] { package }) });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeleteStagingGroupAsync(currentUser, group))
                .ReturnsAsync(StagingGroupDeletionResult.Conflict(1));
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeleteGroupPost(currentUser.Username, group.Id);

            var model = ResultAssert.IsView<DeleteStagingGroupViewModel>(result);
            Assert.Equal(1, model.PackageCount);
            Assert.Contains(
                target.ModelState[string.Empty].Errors,
                error => error.ErrorMessage == "The staging group cannot be deleted while package promotion is active.");
        }

        [Fact]
        public void DisplaysUngroupedPackagesForAnOwner()
        {
            var currentUser = new User("Current") { Key = 1 };
            var ungroupedPackage = CreateStagedPackage(42, "Ungrouped.Package", "1.0.0", currentUser, StagedPackageStatus.Ready);
            var groupedPackage = CreateStagedPackage(43, "Grouped.Package", "2.0.0", currentUser, StagedPackageStatus.Ready);
            groupedPackage.StagedPackageIdentity.StagingGroupKey = 10;
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, "current"))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(new[] { groupedPackage, ungroupedPackage });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(Enumerable.Empty<StagingGroup>().ToList());
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
        public void OffersResendForAStalledUngroupedPromotion()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(42, "Example.Package", "1.0.0", currentUser, StagedPackageStatus.Promoting);
            stagedPackage.ActivePromotionId = System.Guid.NewGuid();
            stagedPackage.PromotionMessageSentDate = System.DateTime.UtcNow.AddHours(-2);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(new[] { stagedPackage });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new List<StagingGroup>());
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Ungrouped(currentUser.Username);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.True(Assert.Single(model.Packages).CanResend);
            Assert.False(model.CanResend);
        }

        [Fact]
        public void DisplaysEmptyUngroupedPageAfterLastPackageLeaves()
        {
            var currentUser = new User("current") { Key = 1 };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagedPackages(currentUser))
                .Returns(System.Array.Empty<StagedPackage>());
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new List<StagingGroup>());
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Ungrouped(currentUser.Username);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.True(model.IsUngrouped);
            Assert.Equal(currentUser.Username, model.Owner);
            Assert.Empty(model.Packages);
        }

        [Fact]
        public void HidesUngroupedPageFromUnauthorizedOwners()
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
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.Group(currentUser.Username, group.Id);

            var model = ResultAssert.IsView<StagingGroupDetailViewModel>(result, viewName: "Group");
            Assert.Equal(
                Enumerable.Range(1, GalleryConstants.DefaultPackageListPageSize + 1).Select(index => $"Package.{index:D2}"),
                model.Packages.Select(package => package.Id));
        }

        [Fact]
        public async Task MovesAnAuthorizedPackageToAGroup()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.MovePackageIdentityAsync(currentUser, stagedPackage.StagedPackageIdentity, group))
                .ReturnsAsync(StagingGroupMembershipResult.Updated);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.MovePackage(
                currentUser.Username,
                "PackageA",
                "1.0.0",
                group.Id);

            ResultAssert.IsRedirectTo(result, "/account/staging/current/groups/release");
        }

        [Fact]
        public void OffersUngroupedAndOtherGroupsAsMoveTargets()
        {
            var currentUser = new User("current") { Key = 1 };
            var currentGroup = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "current",
                Name = "Current",
            };
            var targetGroup = new StagingGroup
            {
                Key = 11,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "target",
                Name = "Target",
            };
            var stagedPackage = CreateStagedPackage(currentUser);
            stagedPackage.StagedPackageIdentity.StagingGroupKey = currentGroup.Key;
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { currentGroup, targetGroup });
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = target.MovePackage(currentUser.Username, "PackageA", "1.0.0");

            var model = ResultAssert.IsView<MoveStagedPackageViewModel>(result);
            Assert.Collection(
                model.Groups,
                group =>
                {
                    Assert.Null(group.Id);
                    Assert.Equal("Ungrouped", group.Name);
                },
                group =>
                {
                    Assert.Equal(targetGroup.Id, group.Id);
                    Assert.Equal(targetGroup.Name, group.Name);
                });
        }

        [Fact]
        public async Task MovesAnAuthorizedPackageToUngrouped()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            var stagedPackage = CreateStagedPackage(currentUser);
            stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
            stagedPackage.StagedPackageIdentity.StagingGroup = group;
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.MovePackageIdentityAsync(currentUser, stagedPackage.StagedPackageIdentity, null))
                .ReturnsAsync(StagingGroupMembershipResult.Updated);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.MovePackage(
                currentUser.Username,
                "PackageA",
                "1.0.0",
                groupId: null);

            ResultAssert.IsRedirectTo(result, "/account/staging/current/ungrouped");
        }

        [Fact]
        public async Task RedisplaysMovePageWhenPackageCannotBeMoved()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            var stagedPackage = CreateStagedPackage(currentUser);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStagingGroups(currentUser))
                .Returns(new[] { group });
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.MovePackageIdentityAsync(currentUser, stagedPackage.StagedPackageIdentity, group))
                .ReturnsAsync(StagingGroupMembershipResult.Conflict);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.MovePackage(
                currentUser.Username,
                "PackageA",
                "1.0.0",
                group.Id);

            var model = ResultAssert.IsView<MoveStagedPackageViewModel>(result);
            Assert.Equal(group.Id, model.GroupId);
            Assert.Contains(
                target.ModelState[string.Empty].Errors,
                error => error.ErrorMessage == "The staged package and symbols cannot be moved while promotion is active.");
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
                .ReturnsAsync(true);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.UpdateListed("PackageA", "1.0.0", listed: true);

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(HttpStatusCode.NoContent, (HttpStatusCode)status.StatusCode);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeletesAuthorizedPackageOrExplainsSymbolRestriction(bool hasSymbols)
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = CreateStagedPackage(currentUser);
            if (hasSymbols)
            {
                stagedPackage.StagedPackageIdentity.CurrentStagedSymbolPackageKey = 100;
            }

            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManage(currentUser, stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeletePackageAsync(stagedPackage))
                .ReturnsAsync(!hasSymbols);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeletePackage("PackageA", "1.0.0");

            Assert.IsType<RedirectResult>(result);
            if (hasSymbols)
            {
                Assert.Equal("Remove the staged symbols before deleting their parent package.", target.TempData["ErrorMessage"]);
            }

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
        public async Task PromotesAuthorizedReadyGroup()
        {
            var currentUser = new User("current") { Key = 1 };
            var group = new StagingGroup
            {
                Key = 10,
                OwnerKey = currentUser.Key,
                Owner = currentUser,
                Id = "release",
                Name = "Release",
            };
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.GetEnabledOwner(currentUser, currentUser.Username))
                .Returns(currentUser);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindStagingGroup(currentUser, group.Id))
                .Returns(group);
            GetMock<IPackageStagingPromotionService>()
                .Setup(x => x.PromoteGroupAsync(currentUser, group))
                .ReturnsAsync(StagingGroupPromotionResult.Accepted);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.PromoteGroup(currentUser.Username, group.Id);

            ResultAssert.IsRedirectTo(result, "/account/staging/current/groups/release");
            GetMock<IPackageStagingPromotionService>().Verify(
                x => x.PromoteGroupAsync(currentUser, group),
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
