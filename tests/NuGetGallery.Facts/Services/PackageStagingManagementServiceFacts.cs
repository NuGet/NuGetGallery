// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class PackageStagingManagementServiceFacts
    {
        public class TheOwnerUiMethods
        {
            [Fact]
            public void ListsStagedPackagesForEnabledPersonalAndOrganizationOwners()
            {
                var currentUser = new User("current") { Key = 1 };
                var organization = new Organization("organization") { Key = 2 };
                var disabledOrganization = new Organization("disabled") { Key = 3 };
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = disabledOrganization });

                var personalPackage = CreateStagedPackage(10, "Personal.Package", "1.0.0", currentUser);
                var organizationPackage = CreateStagedPackage(11, "Organization.Package", "2.0.0", organization);
                var disabledPackage = CreateStagedPackage(12, "Disabled.Package", "3.0.0", disabledOrganization);

                var target = CreateService(
                    new[] { personalPackage, organizationPackage, disabledPackage },
                    owner => owner != disabledOrganization);

                var result = target.GetStagedPackages(currentUser);

                Assert.Equal(
                    new[] { "Organization.Package", "Personal.Package" },
                    result.Select(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Id));
                Assert.Equal(
                    new[] { "organization", "current" },
                    result.Select(stagedPackage => stagedPackage.StagedPackageIdentity.Owner.Username));
            }

            [Fact]
            public void ListsOnlyTheNewestAttemptForEachStagedPackage()
            {
                var currentUser = new User("current") { Key = 1 };
                var previousAttempt = CreateStagedPackage(100, 10, "Test.Package", "1.0.0", currentUser);
                previousAttempt.Status = StagedPackageStatus.Superseded;
                var currentAttempt = CreateStagedPackage(101, 10, "Test.Package", "1.0.0", currentUser);
                SetCurrentAttempt(previousAttempt, currentAttempt);
                currentAttempt.StagedPackageIdentity.Package.Listed = true;

                var target = CreateService(new[] { previousAttempt, currentAttempt }, owner => true);

                var result = target.GetStagedPackages(currentUser);

                Assert.Same(currentAttempt, Assert.Single(result));
            }

            [Fact]
            public void ShowsOnlyRemainingStagedMembersDuringGroupPromotion()
            {
                var owner = new User("owner") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", owner);
                group.ActivePromotionId = Guid.NewGuid();
                var promoting = CreateStagedPackage(100, "Promoting.Package", "1.0.0", owner);
                promoting.Status = StagedPackageStatus.Promoting;
                promoting.StagedPackageIdentity.StagingGroupKey = group.Key;
                var succeeded = CreateStagedPackage(101, "Succeeded.Package", "1.0.0", owner);
                succeeded.Status = StagedPackageStatus.Succeeded;
                succeeded.ActivePromotionId = group.ActivePromotionId;
                succeeded.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                succeeded.StagedPackageIdentity.StagingGroupKey = group.Key;
                var target = CreateService(new[] { promoting, succeeded }, user => true, stagingGroups: new[] { group });

                Assert.Same(promoting, Assert.Single(target.GetStagedPackages(owner)));
                Assert.Same(promoting, Assert.Single(Assert.Single(target.GetStagingGroupSummaries(owner)).Packages));
                Assert.Same(promoting, Assert.Single(Assert.Single(target.GetStagingGroupSummaryPage(owner, 1, 1).Items).Packages));
                var page = target.GetStagingGroupPackagePage(owner, group.Id, page: 1, pageSize: 10);
                Assert.Same(promoting, Assert.Single(page.Items));
                Assert.Equal(1, page.TotalCount);
            }

            [Fact]
            public void ListsGroupsForEnabledPersonalAndOrganizationOwners()
            {
                var currentUser = new User("current") { Key = 1 };
                var organization = new Organization("organization") { Key = 2 };
                var disabledOrganization = new Organization("disabled") { Key = 3 };
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = disabledOrganization });
                var groups = new[]
                {
                    CreateStagingGroup(10, "z-group", "Z group", currentUser),
                    CreateStagingGroup(11, "a-group", "A group", currentUser),
                    CreateStagingGroup(12, "org-group", "Organization group", organization),
                    CreateStagingGroup(13, "disabled-group", "Disabled group", disabledOrganization),
                };
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => owner != disabledOrganization,
                    stagingGroups: groups);

                var result = target.GetStagingGroups(currentUser);

                Assert.Equal(new[] { "a-group", "z-group", "org-group" }, result.Select(group => group.Id));
            }

            [Fact]
            public void FindsAnOwnerVisibleGroupCaseInsensitively()
            {
                var currentUser = new User("Current") { Key = 1 };
                var group = CreateStagingGroup(10, "Test-Group", "Test group", currentUser);
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroups: new[] { group });

                var result = target.FindStagingGroup(currentUser, "test-group");

                Assert.Same(group, result);
            }

            [Fact]
            public void DoesNotFindAGroupForAnotherOwner()
            {
                var currentUser = new User("current") { Key = 1 };
                var otherOwner = new User("other") { Key = 2 };
                var group = CreateStagingGroup(10, "test-group", "Test group", otherOwner);
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroups: new[] { group });

                var result = target.FindStagingGroup(currentUser, group.Id);

                Assert.Null(result);
            }

            [Fact]
            public async Task CreatesAGroupForAnEnabledOwner()
            {
                var before = DateTime.UtcNow;
                var currentUser = new User("current") { Key = 1 };
                var organization = new Organization("organization") { Key = 2 };
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                StagingGroup insertedGroup = null;
                stagingGroupRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagingGroup>()))
                    .Callback<StagingGroup>(group => insertedGroup = group);
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroupRepository: stagingGroupRepository);

                var result = await target.CreateStagingGroupAsync(organization, "release.1", " Release 1 ");

                Assert.Equal(CreateStagingGroupResultType.Created, result.Type);
                Assert.Same(insertedGroup, result.Group);
                Assert.Same(organization, result.Group.Owner);
                Assert.Equal(organization.Key, result.Group.OwnerKey);
                Assert.Equal("release.1", result.Group.Id);
                Assert.Equal("Release 1", result.Group.Name);
                Assert.InRange(result.Group.CreatedDate, before, DateTime.UtcNow);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public void ListsGroupsOnlyForTheOwner()
            {
                var currentUser = new User("current") { Key = 1 };
                var organization = new Organization("organization") { Key = 2 };
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
                var personalGroup = CreateStagingGroup(10, "personal", "Personal", currentUser);
                var organizationGroup = CreateStagingGroup(11, "organization", "Organization", organization);
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroups: new[] { personalGroup, organizationGroup });

                var result = target.GetStagingGroupSummaries(organization);

                Assert.Same(organizationGroup, Assert.Single(result).Group);
            }

            [Fact]
            public void GetsAllCurrentPackagesInTheOwnersGroups()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var firstPackage = CreateStagedPackage(100, "First.Package", "1.0.0", currentUser);
                firstPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                var secondPackage = CreateStagedPackage(101, "Second.Package", "1.0.0", currentUser);
                secondPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                var otherPackage = CreateStagedPackage(102, "Other.Package", "1.0.0", currentUser);
                var target = CreateService(
                    new[] { firstPackage, secondPackage, otherPackage },
                    owner => true,
                    stagingGroups: new[] { group });

                var result = target.GetStagingGroupSummaries(currentUser);

                var summary = Assert.Single(result);
                Assert.Same(group, summary.Group);
                Assert.Equal(new[] { firstPackage, secondPackage }, summary.Packages);
            }

            [Fact]
            public void GetsAnOrderedPageOfGroupsAndOnlyItsPackages()
            {
                var currentUser = new User("current") { Key = 1 };
                var oldestGroup = CreateStagingGroup(10, "oldest", "Oldest", currentUser);
                oldestGroup.CreatedDate = new DateTime(2026, 9, 1);
                var middleGroup = CreateStagingGroup(11, "middle", "Middle", currentUser);
                middleGroup.CreatedDate = new DateTime(2026, 9, 2);
                var newestGroup = CreateStagingGroup(12, "newest", "Newest", currentUser);
                newestGroup.CreatedDate = new DateTime(2026, 9, 3);
                var middlePackage = CreateStagedPackage(100, "Middle.Package", "1.0.0", currentUser);
                middlePackage.StagedPackageIdentity.StagingGroupKey = middleGroup.Key;
                var newestPackage = CreateStagedPackage(101, "Newest.Package", "1.0.0", currentUser);
                newestPackage.StagedPackageIdentity.StagingGroupKey = newestGroup.Key;
                var target = CreateService(
                    new[] { middlePackage, newestPackage },
                    owner => true,
                    stagingGroups: new[] { oldestGroup, middleGroup, newestGroup });

                var result = target.GetStagingGroupSummaryPage(currentUser, page: 2, pageSize: 1);

                Assert.Equal(3, result.TotalCount);
                var summary = Assert.Single(result.Items);
                Assert.Same(middleGroup, summary.Group);
                Assert.Same(middlePackage, Assert.Single(summary.Packages));
            }

            [Fact]
            public void GetsAnOrderedPageOfGroupPackagesWithWholeGroupState()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var olderPackage = CreateStagedPackage(100, "Older.Package", "1.0.0", currentUser);
                olderPackage.UploadedDate = new DateTime(2026, 9, 1);
                olderPackage.Status = StagedPackageStatus.Ready;
                olderPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                var lowerKeyPackage = CreateStagedPackage(101, "LowerKey.Package", "1.0.0", currentUser);
                lowerKeyPackage.UploadedDate = new DateTime(2026, 9, 2);
                lowerKeyPackage.Status = StagedPackageStatus.Ready;
                lowerKeyPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                var higherKeyPackage = CreateStagedPackage(102, "HigherKey.Package", "1.0.0", currentUser);
                higherKeyPackage.UploadedDate = lowerKeyPackage.UploadedDate;
                higherKeyPackage.Status = StagedPackageStatus.Validating;
                higherKeyPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                var unrelatedPackage = CreateStagedPackage(103, "Unrelated.Package", "1.0.0", currentUser);
                unrelatedPackage.UploadedDate = new DateTime(2026, 9, 3);
                var target = CreateService(
                    new[] { olderPackage, lowerKeyPackage, higherKeyPackage, unrelatedPackage },
                    owner => true,
                    stagingGroups: new[] { group });

                var result = target.GetStagingGroupPackagePage(currentUser, group.Id, page: 2, pageSize: 1);

                Assert.Same(group, result.Group);
                Assert.Same(lowerKeyPackage, Assert.Single(result.Items));
                Assert.Equal(3, result.TotalCount);
                Assert.False(result.AllPackagesReady);
            }

            [Fact]
            public async Task UsesTheGroupIdWhenTheNameIsNotProvided()
            {
                var currentUser = new User("current") { Key = 1 };
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroupRepository: stagingGroupRepository);

                var result = await target.CreateStagingGroupAsync(currentUser, "release.1", null);

                Assert.Equal(CreateStagingGroupResultType.Created, result.Type);
                Assert.Equal("release.1", result.Group.Name);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateADuplicateGroup()
            {
                var currentUser = new User("current") { Key = 1 };
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroupRepository: stagingGroupRepository);
                stagingGroupRepository
                    .Setup(x => x.CommitChangesAsync())
                    .ThrowsAsync(new DbUpdateException("Duplicate group.", CreateSqlException(2627)));

                var result = await target.CreateStagingGroupAsync(currentUser, "release.1", "Another name");

                Assert.Equal(CreateStagingGroupResultType.GroupAlreadyExists, result.Type);
                Assert.Null(result.Group);
                stagingGroupRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagingGroup>()), Times.Once);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RenamesAnOwnerGroupWithoutChangingItsIdentity()
            {
                var currentUser = new User("current") { Key = 1 };
                var createdDate = new DateTime(2026, 9, 1);
                var group = CreateStagingGroup(10, "release.1", "Release 1", currentUser);
                group.CreatedDate = createdDate;
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroups: new[] { group },
                    stagingGroupRepository: stagingGroupRepository);

                var result = await target.RenameStagingGroupAsync(currentUser, "RELEASE.1", " Release 2 ");

                Assert.Same(group, result);
                Assert.Equal("Release 2", group.Name);
                Assert.Equal("release.1", group.Id);
                Assert.Same(currentUser, group.Owner);
                Assert.Equal(createdDate, group.CreatedDate);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RejectsRenamingAGroupWhilePromotionIsActive()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release.1", "Release 1", currentUser);
                group.ActivePromotionId = Guid.NewGuid();
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                var target = CreateService(
                    Array.Empty<StagedPackage>(),
                    owner => true,
                    stagingGroups: new[] { group },
                    stagingGroupRepository: stagingGroupRepository);

                var result = await target.RenameStagingGroupAsync(currentUser, group.Id, "Release 2");

                Assert.Null(result);
                Assert.Equal("Release 1", group.Name);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task DeletesAGroupAndItsCurrentStagedPackagesInOneTransaction()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var firstPackage = CreateStagedPackage(100, "First.Package", "1.0.0", currentUser);
                firstPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                firstPackage.StagedPackageIdentity.StagingGroup = group;
                firstPackage.StagedPackageIdentity.Package.Listed = true;
                var secondPackage = CreateStagedPackage(101, "Second.Package", "2.0.0", currentUser);
                secondPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                secondPackage.StagedPackageIdentity.StagingGroup = group;
                secondPackage.StagedPackageIdentity.Package.Listed = true;
                var deletedPackage = CreateStagedPackage(102, "Deleted.Package", "3.0.0", currentUser);
                deletedPackage.Status = StagedPackageStatus.Deleted;
                deletedPackage.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
                deletedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                deletedPackage.StagedPackageIdentity.StagingGroup = group;
                var unrelatedPackage = CreateStagedPackage(103, "Other.Package", "4.0.0", currentUser);
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), PackageStatus.Deleted, false))
                    .Callback<Package, PackageStatus, bool>((package, status, commitChanges) => package.PackageStatusKey = status)
                    .Returns(Task.CompletedTask);
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                var target = CreateService(
                    new[] { firstPackage, secondPackage, deletedPackage, unrelatedPackage },
                    owner => true,
                    packageService: packageService.Object,
                    stagingGroups: new[] { group },
                    stagingGroupRepository: stagingGroupRepository);

                var result = await target.DeleteStagingGroupAsync(currentUser, group);

                Assert.Equal(StagingGroupDeletionResultType.Deleted, result.Type);
                Assert.Equal(2, result.AffectedPackageCount);
                Assert.All(new[] { firstPackage, secondPackage }, stagedPackage =>
                {
                    Assert.Equal(StagedPackageStatus.Deleted, stagedPackage.Status);
                    Assert.Equal(PackageStatus.Deleted, stagedPackage.StagedPackageIdentity.Package.PackageStatusKey);
                    Assert.False(stagedPackage.StagedPackageIdentity.Package.Listed);
                    Assert.Null(stagedPackage.StagedPackageIdentity.StagingGroupKey);
                    Assert.Null(stagedPackage.StagedPackageIdentity.StagingGroup);
                });
                Assert.Equal(StagedPackageStatus.Deleted, deletedPackage.Status);
                Assert.Equal(PackageStatus.Deleted, deletedPackage.StagedPackageIdentity.Package.PackageStatusKey);
                Assert.Null(deletedPackage.StagedPackageIdentity.StagingGroupKey);
                Assert.Null(deletedPackage.StagedPackageIdentity.StagingGroup);
                Assert.Equal(StagedPackageStatus.Validating, unrelatedPackage.Status);
                packageService.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), PackageStatus.Deleted, false),
                    Times.Exactly(2));
                stagingGroupRepository.Verify(x => x.DeleteOnCommit(group), Times.Once);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
                stagingGroupRepository.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Once);
            }

            [Fact]
            public async Task RejectsDeletingAGroupBeforeMutatingAnyPackageWhenPromotionIsActive()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                group.ActivePromotionId = Guid.NewGuid();
                var readyPackage = CreateStagedPackage(100, "Ready.Package", "1.0.0", currentUser);
                readyPackage.Status = StagedPackageStatus.Ready;
                readyPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                readyPackage.StagedPackageIdentity.StagingGroup = group;
                var promotingPackage = CreateStagedPackage(101, "Promoting.Package", "2.0.0", currentUser);
                promotingPackage.Status = StagedPackageStatus.Promoting;
                promotingPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                promotingPackage.StagedPackageIdentity.StagingGroup = group;
                var packageService = new Mock<IPackageService>();
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                var target = CreateService(
                    new[] { readyPackage, promotingPackage },
                    owner => true,
                    packageService: packageService.Object,
                    stagingGroups: new[] { group },
                    stagingGroupRepository: stagingGroupRepository);

                var result = await target.DeleteStagingGroupAsync(currentUser, group);

                Assert.Equal(StagingGroupDeletionResultType.Conflict, result.Type);
                Assert.Equal(2, result.AffectedPackageCount);
                Assert.Equal(StagedPackageStatus.Ready, readyPackage.Status);
                Assert.Equal(group.Key, readyPackage.StagedPackageIdentity.StagingGroupKey);
                Assert.Equal(StagedPackageStatus.Promoting, promotingPackage.Status);
                Assert.Equal(group.Key, promotingPackage.StagedPackageIdentity.StagingGroupKey);
                packageService.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);
                stagingGroupRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagingGroup>()), Times.Never);
                stagingGroupRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task MovesAStagedPackageIdentityToAGroup()
            {
                var currentUser = new User("current") { Key = 1 };
                var previousGroup = CreateStagingGroup(10, "previous", "Previous", currentUser);
                var targetGroup = CreateStagingGroup(11, "target", "Target", currentUser);
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = previousGroup.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = previousGroup;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.AddPackageToStagingGroupAsync(currentUser, targetGroup, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Updated, result);
                Assert.Equal(targetGroup.Key, stagedPackage.StagedPackageIdentity.StagingGroupKey);
                Assert.Same(targetGroup, stagedPackage.StagedPackageIdentity.StagingGroup);
                Assert.Equal(1, stagedPackage.MutationRevision);
                Assert.Equal(1, previousGroup.MutationRevision);
                Assert.Equal(1, targetGroup.MutationRevision);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task TreatsExistingGroupMembershipAsANoOp()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.AddPackageToStagingGroupAsync(currentUser, group, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Unchanged, result);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task RejectsMovingAPromotingPackage()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = StagedPackageStatus.Promoting;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.AddPackageToStagingGroupAsync(currentUser, group, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Conflict, result);
                Assert.Null(stagedPackage.StagedPackageIdentity.StagingGroupKey);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task RejectsMovingAPackageIntoAnActiveGroup()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                group.ActivePromotionId = Guid.NewGuid();
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = StagedPackageStatus.Ready;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.AddPackageToStagingGroupAsync(currentUser, group, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Conflict, result);
                Assert.Null(stagedPackage.StagedPackageIdentity.StagingGroupKey);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task RejectsMovingWhenPromotionWinsTheRace()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

                var result = await target.AddPackageToStagingGroupAsync(currentUser, group, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Conflict, result);
                Assert.Equal(1, stagedPackage.MutationRevision);
                Assert.Equal(1, group.MutationRevision);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RemovesAStagedPackageIdentityFromAGroup()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.RemovePackageFromStagingGroupAsync(currentUser, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Updated, result);
                Assert.Null(stagedPackage.StagedPackageIdentity.StagingGroupKey);
                Assert.Null(stagedPackage.StagedPackageIdentity.StagingGroup);
                Assert.Equal(1, stagedPackage.MutationRevision);
                Assert.Equal(1, group.MutationRevision);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task ReturnsConflictWhenPromotionWinsRemovingAGroupMember()
            {
                var owner = new User("owner") { Key = 1 };
                var group = CreateStagingGroup(20, "release", "Release", owner);
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    stagedPackageRepository: stagedPackageRepository);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

                var result = await target.RemovePackageFromStagingGroupAsync(owner, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Conflict, result);
                Assert.Equal(1, stagedPackage.MutationRevision);
                Assert.Equal(1, group.MutationRevision);
            }

            [Fact]
            public async Task RejectsRemovingAPromotingPackageFromAGroup()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = StagedPackageStatus.Promoting;
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.RemovePackageFromStagingGroupAsync(currentUser, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Conflict, result);
                Assert.Equal(group.Key, stagedPackage.StagedPackageIdentity.StagingGroupKey);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task RejectsRemovingAPackageFromAnActiveGroup()
            {
                var currentUser = new User("current") { Key = 1 };
                var group = CreateStagingGroup(10, "release", "Release", currentUser);
                group.ActivePromotionId = Guid.NewGuid();
                var stagedPackage = CreateStagedPackage(100, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = StagedPackageStatus.Succeeded;
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.RemovePackageFromStagingGroupAsync(currentUser, stagedPackage);

                Assert.Equal(StagingGroupMembershipResult.Conflict, result);
                Assert.Equal(group.Key, stagedPackage.StagedPackageIdentity.StagingGroupKey);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public void ListsOnlyNewestApiKeyAuthorizedAttempts()
            {
                var currentUser = new User("current") { Key = 1 };
                var scopes = Array.Empty<Scope>();
                var previousAttempt = CreateStagedPackage(100, 10, "Test.Package", "1.0.0", currentUser);
                previousAttempt.Status = StagedPackageStatus.Superseded;
                var currentAttempt = CreateStagedPackage(101, 10, "Test.Package", "1.0.0", currentUser);
                SetCurrentAttempt(previousAttempt, currentAttempt);
                currentAttempt.Status = StagedPackageStatus.Ready;
                currentAttempt.StagedPackageIdentity.Package.Listed = true;
                var hiddenPackage = CreateStagedPackage(102, 11, "Hidden.Package", "2.0.0", currentUser);
                var authorizationService = new Mock<IPackageStagingAuthorizationService>();
                authorizationService
                    .Setup(x => x.GetEnabledOwners(currentUser))
                    .Returns(new[] { currentUser });
                authorizationService
                    .Setup(x => x.CanManageWithApiKey(currentUser, scopes, currentAttempt))
                    .Returns(true);
                var target = CreateService(
                    new[] { previousAttempt, currentAttempt, hiddenPackage },
                    owner => true,
                    authorizationService.Object);

                var result = target.GetPackages(currentUser, scopes);

                var package = Assert.Single(result);
                Assert.Equal("Test.Package", package.Id);
                Assert.Equal("1.0.0", package.Version);
                Assert.Equal(StagedPackageStatus.Ready.ToString(), package.Status);
                Assert.True(package.Listed);
            }

            [Fact]
            public void GetsTheNewestAttemptForAPackage()
            {
                var currentUser = new User("current") { Key = 1, EmailAddress = "current@example.test" };
                var scopes = Array.Empty<Scope>();
                var previousOwner = new User("previous") { Key = 2 };
                var previousAttempt = CreateStagedPackage(100, 10, "Test.Package", "1.0.0", previousOwner);
                var currentAttempt = CreateStagedPackage(101, 10, "Test.Package", "1.0.0", currentUser);
                SetCurrentAttempt(previousAttempt, currentAttempt);
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(currentAttempt.StagedPackageIdentity.Package);
                var authorizationService = new Mock<IPackageStagingAuthorizationService>();
                authorizationService
                    .Setup(x => x.CanManageWithApiKey(It.IsAny<User>(), scopes, currentAttempt))
                    .Returns(true);
                var target = CreateService(
                    new[] { previousAttempt, currentAttempt },
                    owner => true,
                    authorizationService.Object,
                    packageService.Object);

                var result = target.GetPackageStatus(currentUser, scopes, "Test.Package", "1.0.0");

                Assert.NotNull(result);
                Assert.True(result.Listed);
            }

            [Fact]
            public void DoesNotGetADeletedPackage()
            {
                var currentUser = new User("current") { Key = 1 };
                var stagedPackage = CreateStagedPackage(100, 10, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = StagedPackageStatus.Deleted;
                stagedPackage.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0"))
                    .Returns(stagedPackage.StagedPackageIdentity.Package);
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    packageService: packageService.Object);

                var result = target.GetPackageStatus(currentUser, Array.Empty<Scope>(), "Test.Package", "1.0.0");

                Assert.Null(result);
            }

            [Theory]
            [InlineData(StagedPackageStatus.Validating, "uploaded", "uploaded-etag")]
            [InlineData(StagedPackageStatus.FailedValidation, "uploaded", "uploaded-etag")]
            [InlineData(StagedPackageStatus.Ready, "validated", "validated-etag")]
            public async Task OpensExpectedContentForCurrentAttempt(StagedPackageStatus status, string expectedPath, string expectedETag)
            {
                var currentUser = new User("current") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = status;
                stagedPackage.UploadedBlobPath = "uploaded";
                stagedPackage.UploadedBlobETag = "uploaded-etag";
                stagedPackage.ValidatedBlobPath = "validated";
                stagedPackage.ValidatedBlobETag = "validated-etag";
                var expected = new MemoryStream();
                var stagingBlobService = new Mock<IStagingBlobService>();
                stagingBlobService
                    .Setup(x => x.OpenPackageFileAsync(expectedPath, expectedETag))
                    .ReturnsAsync(expected);
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagingBlobService: stagingBlobService.Object);

                var actual = await target.OpenPackageContentAsync(stagedPackage);

                Assert.Same(expected, actual);
            }

            [Fact]
            public void DoesNotFindADeletedAttempt()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.Status = StagedPackageStatus.Deleted;
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0"))
                    .Returns(stagedPackage.StagedPackageIdentity.Package);
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    packageService: packageService.Object);

                var result = target.FindCurrentStagedPackage("Test.Package", "1.0.0");

                Assert.Null(result);
            }

            [Fact]
            public async Task UpdatesListedIntent()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.StagedPackageIdentity.Package.Listed = false;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.UpdateListedAsync(stagedPackage, listed: true);

                Assert.True(result);
                Assert.True(stagedPackage.StagedPackageIdentity.Package.Listed);
                Assert.Equal(1, stagedPackage.MutationRevision);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RejectsUpdatingListedIntentWhileGroupPromotionIsActive()
            {
                var owner = new User("owner") { Key = 1 };
                var group = CreateStagingGroup(20, "release", "Release", owner);
                group.ActivePromotionId = Guid.NewGuid();
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                stagedPackage.StagedPackageIdentity.Package.Listed = false;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.UpdateListedAsync(stagedPackage, listed: true);

                Assert.False(result);
                Assert.False(stagedPackage.StagedPackageIdentity.Package.Listed);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task UpdatingGroupedListedIntentUpdatesPackageAndGroupRevisions()
            {
                var owner = new User("owner") { Key = 1 };
                var group = CreateStagingGroup(20, "release", "Release", owner);
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var target = CreateService(new[] { stagedPackage }, user => true);

                var result = await target.UpdateListedAsync(stagedPackage, listed: true);

                Assert.True(result);
                Assert.True(stagedPackage.StagedPackageIdentity.Package.Listed);
                Assert.Equal(1, stagedPackage.MutationRevision);
                Assert.Equal(1, group.MutationRevision);
            }

            [Fact]
            public async Task ReturnsConflictWhenPromotionWinsUpdatingListedIntent()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    stagedPackageRepository: stagedPackageRepository);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

                var result = await target.UpdateListedAsync(stagedPackage, listed: true);

                Assert.False(result);
                Assert.Equal(1, stagedPackage.MutationRevision);
            }

            [Fact]
            public async Task DeletesCurrentAttemptAndRetainsDeletedPackage()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.StagedPackageIdentity.Package.Listed = true;
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.UpdatePackageStatusAsync(stagedPackage.StagedPackageIdentity.Package, PackageStatus.Deleted, false))
                    .Callback(() => stagedPackage.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted)
                    .Returns(Task.CompletedTask);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    packageService: packageService.Object,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.DeletePackageAsync(stagedPackage);

                Assert.True(result);
                Assert.Equal(StagedPackageStatus.Deleted, stagedPackage.Status);
                Assert.Equal(PackageStatus.Deleted, stagedPackage.StagedPackageIdentity.Package.PackageStatusKey);
                Assert.False(stagedPackage.StagedPackageIdentity.Package.Listed);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task DeletingGroupedPackageUpdatesGroupRevision()
            {
                var owner = new User("owner") { Key = 1 };
                var group = CreateStagingGroup(20, "release", "Release", owner);
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var target = CreateService(new[] { stagedPackage }, user => true);

                var result = await target.DeletePackageAsync(stagedPackage);

                Assert.True(result);
                Assert.Equal(StagedPackageStatus.Deleted, stagedPackage.Status);
                Assert.Equal(0, stagedPackage.MutationRevision);
                Assert.Equal(1, group.MutationRevision);
            }

            [Fact]
            public async Task ReturnsConflictWhenPackageChangesBeforeDeletionIsSaved()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    stagedPackageRepository: stagedPackageRepository);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

                var result = await target.DeletePackageAsync(stagedPackage);

                Assert.False(result);
            }

            [Fact]
            public async Task RejectsDeletingAnUngroupedPromotingPackage()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.Status = StagedPackageStatus.Promoting;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.DeletePackageAsync(stagedPackage);

                Assert.False(result);
                Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task RejectsDeletingAPackageWhileGroupPromotionIsActive()
            {
                var owner = new User("owner") { Key = 1 };
                var group = CreateStagingGroup(20, "release", "Release", owner);
                group.ActivePromotionId = Guid.NewGuid();
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.Status = StagedPackageStatus.Promoting;
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var packageService = new Mock<IPackageService>();
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    packageService: packageService.Object,
                    stagedPackageRepository: stagedPackageRepository);

                var result = await target.DeletePackageAsync(stagedPackage);

                Assert.False(result);
                Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
                packageService.Verify(
                    x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()),
                    Times.Never);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            private static PackageStagingManagementService CreateService(
                IEnumerable<StagedPackage> stagedPackages,
                Func<User, bool> isEnabled,
                IPackageStagingAuthorizationService authorizationService = null,
                IPackageService packageService = null,
                IStagingBlobService stagingBlobService = null,
                Mock<IEntityRepository<StagedPackage>> stagedPackageRepository = null,
                IEnumerable<StagingGroup> stagingGroups = null,
                Mock<IEntityRepository<StagingGroup>> stagingGroupRepository = null)
            {
                var stagedPackagesList = stagedPackages.ToList();
                var stagedPackagesQuery = stagedPackagesList.AsQueryable();
                var stagedPackagesSet = new Mock<DbSet<StagedPackage>>();
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.Provider).Returns(stagedPackagesQuery.Provider);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.Expression).Returns(stagedPackagesQuery.Expression);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.ElementType).Returns(stagedPackagesQuery.ElementType);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.GetEnumerator()).Returns(() => stagedPackagesQuery.GetEnumerator());
                stagedPackagesSet.Setup(x => x.Include("StagedPackageIdentity.Package")).Returns(stagedPackagesSet.Object);
                stagedPackagesSet.Setup(x => x.Include("StagedPackageIdentity.Package.PackageRegistration")).Returns(stagedPackagesSet.Object);
                stagedPackagesSet.Setup(x => x.Include("StagedPackageIdentity.Owner")).Returns(stagedPackagesSet.Object);
                stagedPackagesSet.Setup(x => x.Include("StagedPackageIdentity.StagingGroup")).Returns(stagedPackagesSet.Object);
                stagedPackageRepository = stagedPackageRepository ?? new Mock<IEntityRepository<StagedPackage>>();
                stagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(stagedPackagesSet.Object);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Returns(Task.CompletedTask);
                stagedPackageRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                    .Returns((Func<Task> action) => action());
                var stagingGroupsList = (stagingGroups ?? Array.Empty<StagingGroup>()).ToList();
                var stagingGroupsQuery = stagingGroupsList.AsQueryable();
                var stagingGroupsSet = new Mock<DbSet<StagingGroup>>();
                stagingGroupsSet.As<IQueryable<StagingGroup>>().Setup(x => x.Provider).Returns(stagingGroupsQuery.Provider);
                stagingGroupsSet.As<IQueryable<StagingGroup>>().Setup(x => x.Expression).Returns(stagingGroupsQuery.Expression);
                stagingGroupsSet.As<IQueryable<StagingGroup>>().Setup(x => x.ElementType).Returns(stagingGroupsQuery.ElementType);
                stagingGroupsSet.As<IQueryable<StagingGroup>>().Setup(x => x.GetEnumerator()).Returns(() => stagingGroupsQuery.GetEnumerator());
                stagingGroupsSet.Setup(x => x.Include("Owner")).Returns(stagingGroupsSet.Object);
                stagingGroupRepository = stagingGroupRepository ?? new Mock<IEntityRepository<StagingGroup>>();
                stagingGroupRepository
                    .Setup(x => x.GetAll())
                    .Returns(stagingGroupsSet.Object);
                stagingGroupRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Returns(Task.CompletedTask);
                stagingGroupRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                    .Returns((Func<Task> action) => action());

                var defaultAuthorizationService = new Mock<IPackageStagingAuthorizationService>();
                defaultAuthorizationService
                    .Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedPackage>()))
                    .Returns(true);
                defaultAuthorizationService
                    .Setup(x => x.GetEnabledOwners(It.IsAny<User>()))
                    .Returns((User currentUser) =>
                        new[] { currentUser }
                            .Concat(currentUser.Organizations.Select(membership => membership.Organization))
                            .Where(isEnabled)
                            .OrderBy(owner => owner.Username)
                            .ToList());

                return new PackageStagingManagementService(
                    authorizationService ?? defaultAuthorizationService.Object,
                    packageService ?? Mock.Of<IPackageService>(),
                    stagedPackageRepository.Object,
                    stagingGroupRepository.Object,
                    stagingBlobService ?? Mock.Of<IStagingBlobService>());
            }

            private static StagingGroup CreateStagingGroup(int key, string id, string name, User owner)
            {
                return new StagingGroup
                {
                    Key = key,
                    Id = id,
                    Name = name,
                    OwnerKey = owner.Key,
                    Owner = owner,
                };
            }

            private static StagedPackage CreateStagedPackage(int packageKey, string id, string version, User owner)
            {
                return CreateStagedPackage(packageKey, packageKey, id, version, owner);
            }

            private static StagedPackage CreateStagedPackage(int key, int packageKey, string id, string version, User owner)
            {
                var registration = new PackageRegistration { Id = id };
                registration.Owners.Add(owner);
                var package = new Package
                {
                    Key = packageKey,
                    NormalizedVersion = version,
                    PackageRegistration = registration,
                    PackageStatusKey = PackageStatus.Staged,
                };

                var stagedPackageIdentity = new StagedPackageIdentity
                {
                    Package = package,
                    OwnerKey = owner.Key,
                    Owner = owner,
                    Key = packageKey,
                };
                var stagedPackage = new StagedPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = stagedPackageIdentity.Key,
                    StagedPackageIdentity = stagedPackageIdentity,
                    UploadedDate = DateTime.UtcNow,
                };
                SetCurrentAttempt(stagedPackage);
                return stagedPackage;
            }

            private static void SetCurrentAttempt(StagedPackage stagedPackage)
            {
                stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey = stagedPackage.Key;
                stagedPackage.StagedPackageIdentity.CurrentStagedPackage = stagedPackage;
            }

            private static void SetCurrentAttempt(StagedPackage previousAttempt, StagedPackage currentAttempt)
            {
                previousAttempt.StagedPackageIdentity = currentAttempt.StagedPackageIdentity;
                previousAttempt.StagedPackageIdentityKey = currentAttempt.StagedPackageIdentityKey;
                SetCurrentAttempt(currentAttempt);
            }

            private static SqlException CreateSqlException(int number)
            {
                var error = Activator.CreateInstance(
                    typeof(SqlError),
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    args: new object[] { number, (byte)2, (byte)3, "server", "error", "procedure", 4 },
                    culture: null);
                var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true);
                typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(errors, new[] { error });

                return (SqlException)typeof(SqlException)
                    .GetMethod(
                        "CreateException",
                        BindingFlags.Static | BindingFlags.NonPublic,
                        binder: null,
                        types: new[] { typeof(SqlErrorCollection), typeof(string) },
                        modifiers: null)
                    .Invoke(null, new object[] { errors, "16.0" });
            }
        }
    }
}
