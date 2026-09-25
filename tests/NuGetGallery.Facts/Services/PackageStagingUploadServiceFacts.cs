// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery
{
    public class PackageStagingUploadServiceFacts
    {
        public class TheStagePackageAsyncMethod
        {
            [Theory]
            [InlineData(StagedPackageStatus.Validating, false, false)]
            [InlineData(StagedPackageStatus.Ready, true, false)]
            [InlineData(StagedPackageStatus.Validating, true, true)]
            public async Task StagesPackage(StagedPackageStatus expectedStatus, bool assignGroup, bool createGroup)
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23, EmailAddress = "owner@example.com" };
                var scopes = new List<Scope>();
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "PackageA" },
                    NormalizedVersion = "1.0.0",
                };
                var file = new StagingFileReference("packagea/1.0.0/file.nupkg", "etag");
                var group = new StagingGroup { Key = 47, Id = "release", OwnerKey = owner.Key };
                var managementService = new Mock<IPackageStagingManagementService>();
                managementService.Setup(x => x.FindStagingGroup(owner, "release")).Returns(createGroup ? null : group);

                var apiScopeEvaluator = new Mock<IApiScopeEvaluator>(MockBehavior.Strict);
                apiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        It.IsAny<User>(),
                        It.IsAny<IEnumerable<Scope>>(),
                        It.IsAny<IActionRequiringEntityPermissions<ActionOnNewPackageContext>>(),
                        It.IsAny<ActionOnNewPackageContext>(),
                        It.IsAny<string[]>()))
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);
                packageService
                    .Setup(x => x.UpdatePackageStatusAsync(package, PackageStatus.Staged, false))
                    .Callback(() => package.PackageStatusKey = PackageStatus.Staged)
                    .Returns(Task.CompletedTask);

                PackageStreamMetadata streamMetadata = null;
                var packageUploadService = new Mock<IPackageUploadService>();
                packageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageMetadata>(),
                        currentUser))
                    .ReturnsAsync(PackageValidationResult.Accepted());
                packageUploadService
                    .Setup(x => x.GeneratePackageAsync(
                        "PackageA",
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        owner,
                        currentUser))
                    .Callback<string, PackageArchiveReader, PackageStreamMetadata, User, User>(
                        (id, reader, metadata, packageOwner, uploader) => streamMetadata = metadata)
                    .ReturnsAsync(package);
                packageUploadService
                    .Setup(x => x.ValidateAfterGeneratePackageAsync(
                        package,
                        It.IsAny<PackageArchiveReader>(),
                        owner,
                        currentUser,
                        true))
                    .ReturnsAsync(PackageValidationResult.Accepted());

                var securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService
                    .Setup(x => x.EvaluateUserPoliciesAsync(
                        SecurityPolicyAction.PackagePush,
                        currentUser,
                        It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);
                securityPolicyService
                    .Setup(x => x.EvaluatePackagePoliciesAsync(
                        SecurityPolicyAction.PackagePush,
                        package,
                        currentUser,
                        owner,
                        It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);

                var stagingFiles = new Mock<IStagingBlobService>();
                stagingFiles
                    .Setup(x => x.SavePackageFileAsync("PackageA", "1.0.0", It.IsAny<Stream>()))
                    .ReturnsAsync(file);

                StagedPackage stagedPackage = null;
                StagingGroup createdGroup = null;
                var operations = new List<string>();
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                stagingGroupRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagingGroup>()))
                    .Callback<StagingGroup>(value =>
                    {
                        createdGroup = value;
                        operations.Add("insert group");
                    });
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                stagedPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => stagedPackage = value);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Callback(() =>
                    {
                        operations.Add("save");
                        stagedPackage.Key = 43;
                        if (createdGroup != null)
                        {
                            createdGroup.Key = 48;
                            stagedPackage.StagedPackageIdentity.StagingGroupKey = createdGroup.Key;
                        }
                    })
                    .Returns(Task.CompletedTask);

                var stagedValidationMessageEmitter = new Mock<IStagedPackageValidationMessageEmitter>();
                stagedValidationMessageEmitter
                    .Setup(x => x.StartValidationAsync(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value =>
                    {
                        Assert.Equal(43, value.Key);
                        operations.Add("enqueue");
                    })
                    .ReturnsAsync(expectedStatus);

                stagedPackageRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<System.Func<Task>>()))
                    .Returns<System.Func<Task>>(async action =>
                    {
                        operations.Add("begin");
                        await action();
                        operations.Add("commit");
                    });

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(owner))
                    .Returns(true);

                var target = new PackageStagingUploadService(
                    apiScopeEvaluator.Object,
                    featureFlagService.Object,
                    packageService.Object,
                    managementService.Object,
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingFiles.Object,
                    stagedPackageRepository.Object,
                    stagingGroupRepository.Object,
                    stagedValidationMessageEmitter.Object);

                using (var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0"))
                {
                    var result = await target.StagePackageAsync(
                        currentUser,
                        scopes,
                        Mock.Of<HttpContextBase>(),
                        packageFile,
                        assignGroup ? "release" : null,
                        listed: false);

                    Assert.True(result.Success, result.ErrorMessage);
                    Assert.Equal(HttpStatusCode.Created, result.StatusCode);
                }

                Assert.Equal(PackageStatus.Staged, package.PackageStatusKey);
                Assert.Equal(owner.Key, stagedPackage.StagedPackageIdentity.OwnerKey);
                Assert.Equal(assignGroup ? (createGroup ? createdGroup.Key : group.Key) : (int?)null, stagedPackage.StagedPackageIdentity.StagingGroupKey);
                Assert.False(package.Listed);
                Assert.Equal(assignGroup && !createGroup ? 1L : 0L, group.MutationRevision);
                if (createGroup)
                {
                    Assert.Equal("release", createdGroup.Id);
                    Assert.Equal("release", createdGroup.Name);
                    Assert.Equal(owner.Key, createdGroup.OwnerKey);
                    Assert.Same(owner, createdGroup.Owner);
                    Assert.Same(createdGroup, stagedPackage.StagedPackageIdentity.StagingGroup);
                }
                Assert.Equal(file.Path, stagedPackage.UploadedBlobPath);
                Assert.Equal(file.ETag, stagedPackage.UploadedBlobETag);
                Assert.Equal(streamMetadata.Hash, stagedPackage.UploadHash);
                Assert.Equal(stagedPackage.Key, stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey);
                Assert.Same(stagedPackage, stagedPackage.StagedPackageIdentity.CurrentStagedPackage);
                Assert.Null(stagedPackage.ValidatedBlobPath);
                Assert.Null(stagedPackage.ValidatedBlobETag);
                Assert.Equal(expectedStatus, stagedPackage.Status);
                var expectedOperations = expectedStatus == StagedPackageStatus.Validating
                    ? new[] { "begin", "save", "save", "enqueue", "commit" }
                    : new[] { "begin", "save", "save", "enqueue", "save", "commit" };
                if (createGroup)
                {
                    expectedOperations = new[] { "begin", "insert group", "save", "save", "enqueue", "commit" };
                }
                Assert.Equal(expectedOperations, operations);
                stagingGroupRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagingGroup>()), createGroup ? Times.Once() : Times.Never());
                stagedValidationMessageEmitter.Verify(x => x.StartValidationAsync(stagedPackage), Times.Once);
                stagedPackageRepository.Verify(x => x.InsertOnCommit(stagedPackage), Times.Once);
                stagedPackageRepository.Verify(
                    x => x.CommitChangesAsync(),
                    expectedStatus == StagedPackageStatus.Validating ? Times.Exactly(2) : Times.Exactly(3));
                stagedPackageRepository.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<System.Func<Task>>()), Times.Once);
            }

            [Theory]
            [InlineData(StagedPackageStatus.Validating, HttpStatusCode.OK, true, false, false)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, true, false, false)]
            [InlineData(StagedPackageStatus.FailedValidation, HttpStatusCode.OK, true, false, false)]
            [InlineData(StagedPackageStatus.PromotionFailed, HttpStatusCode.OK, true, false, false)]
            [InlineData(StagedPackageStatus.Superseded, HttpStatusCode.Conflict, true, false, false)]
            [InlineData(StagedPackageStatus.Deleted, HttpStatusCode.OK, true, false, false)]
            [InlineData(StagedPackageStatus.Validating, HttpStatusCode.OK, false, false, false)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, false, false, false)]
            [InlineData(StagedPackageStatus.FailedValidation, HttpStatusCode.OK, false, false, false)]
            [InlineData(StagedPackageStatus.PromotionFailed, HttpStatusCode.OK, false, false, false)]
            [InlineData(StagedPackageStatus.Superseded, HttpStatusCode.Conflict, false, false, false)]
            [InlineData(StagedPackageStatus.Deleted, HttpStatusCode.OK, false, false, false)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, true, true, false)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, false, true, false)]
            [InlineData(StagedPackageStatus.Deleted, HttpStatusCode.OK, false, true, false)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, true, true, true)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, false, true, true)]
            [InlineData(StagedPackageStatus.Deleted, HttpStatusCode.OK, false, true, true)]
            public async Task UploadReturnsExpectedStatus(StagedPackageStatus status, HttpStatusCode expectedStatusCode, bool identical, bool assignGroup, bool createGroup)
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23, EmailAddress = "owner@example.com" };
                var scopes = new List<Scope>();
                var registration = new PackageRegistration { Id = "PackageA" };
                var package = new Package
                {
                    Key = 29,
                    PackageRegistration = registration,
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Staged,
                };
                if (status == StagedPackageStatus.Deleted)
                {
                    package.PackageStatusKey = PackageStatus.Deleted;
                }

                using var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0");
                var uploadHash = CryptographyService.GenerateHash(packageFile, CoreConstants.Sha512HashAlgorithmId);
                packageFile.Position = 0;

                var stagedPackage = new StagedPackage
                {
                    Key = 31,
                    StagedPackageIdentityKey = package.Key,
                    StagedPackageIdentity = new StagedPackageIdentity
                    {
                        Key = package.Key,
                        Package = package,
                        OwnerKey = owner.Key,
                        Owner = owner,
                    },
                    UploadedBlobPath = "old.nupkg",
                    UploadedBlobETag = "old-etag",
                    UploadHash = uploadHash,
                    Status = status,
                };
                stagedPackage.StagedPackageIdentity.CurrentStagedPackageKey = stagedPackage.Key;
                stagedPackage.StagedPackageIdentity.CurrentStagedPackage = stagedPackage;
                if (!identical)
                {
                    stagedPackage.UploadHash = "different";
                }
                var originalGroup = new StagingGroup { Key = 37, Id = "original", OwnerKey = owner.Key };
                stagedPackage.StagedPackageIdentity.StagingGroupKey = originalGroup.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = originalGroup;
                var requestedGroup = new StagingGroup { Key = 38, Id = "release", OwnerKey = owner.Key };
                var managementService = new Mock<IPackageStagingManagementService>();
                managementService.Setup(x => x.FindStagingGroup(owner, "release")).Returns(createGroup ? null : requestedGroup);

                var apiScopeEvaluator = new Mock<IApiScopeEvaluator>(MockBehavior.Strict);
                apiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        scopes,
                        It.IsAny<IActionRequiringEntityPermissions<PackageRegistration>>(),
                        registration,
                        It.IsAny<string[]>()))
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService
                    .Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);
                packageService
                    .Setup(x => x.FindPackageRegistrationById("PackageA"))
                    .Returns(registration);
                packageService
                    .Setup(x => x.GetPackageStatus("PackageA", It.Is<NuGet.Versioning.NuGetVersion>(value => value.ToNormalizedString() == "1.0.0")))
                    .Returns(package.PackageStatusKey);
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("PackageA", "1.0.0"))
                    .Returns(package);
                packageService
                    .Setup(x => x.EnrichPackageFromNuGetPackage(
                        It.IsAny<Package>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageMetadata>(),
                        It.IsAny<PackageStreamMetadata>(),
                        currentUser))
                    .Returns((Package value, PackageArchiveReader reader, PackageMetadata metadata, PackageStreamMetadata streamMetadata, User user) =>
                    {
                        value.PackageRegistration = registration;
                        value.NormalizedVersion = metadata.Version.ToNormalizedString();
                        return value;
                    });
                packageService
                    .Setup(x => x.ReplacePackageMetadataForStagedPackage(
                        stagedPackage,
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageMetadata>(),
                        It.IsAny<PackageStreamMetadata>(),
                        currentUser))
                    .Returns(package);
                packageService
                    .Setup(x => x.UpdatePackageStatusAsync(package, PackageStatus.Staged, false))
                    .Callback(() => package.PackageStatusKey = PackageStatus.Staged)
                    .Returns(Task.CompletedTask);

                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                StagingGroup createdGroup = null;
                var stagingGroupRepository = new Mock<IEntityRepository<StagingGroup>>();
                stagingGroupRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagingGroup>()))
                    .Callback<StagingGroup>(value => createdGroup = value);
                StagedPackage successor = null;
                stagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(new[] { stagedPackage }.AsQueryable());
                stagedPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => successor = value);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Callback(() =>
                    {
                        if (createdGroup != null && createdGroup.Key == 0)
                        {
                            createdGroup.Key = 39;
                            stagedPackage.StagedPackageIdentity.StagingGroupKey = createdGroup.Key;
                        }

                        if (successor != null && successor.Key == 0)
                        {
                            successor.Key = 32;
                        }
                    })
                    .Returns(Task.CompletedTask);
                stagedPackageRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<System.Func<Task>>()))
                    .Returns<System.Func<Task>>(action => action());

                var securityPolicyService = new Mock<ISecurityPolicyService>(MockBehavior.Strict);
                securityPolicyService
                    .Setup(x => x.EvaluateUserPoliciesAsync(
                        SecurityPolicyAction.PackagePush,
                        currentUser,
                        It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);
                securityPolicyService
                    .Setup(x => x.EvaluatePackagePoliciesAsync(
                        SecurityPolicyAction.PackagePush,
                        It.IsAny<Package>(),
                        currentUser,
                        owner,
                        It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);

                var packageUploadService = new Mock<IPackageUploadService>(MockBehavior.Strict);
                packageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), currentUser))
                    .ReturnsAsync(PackageValidationResult.Accepted());
                packageUploadService
                    .Setup(x => x.ValidateAfterGeneratePackageAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), owner, currentUser, false))
                    .ReturnsAsync(PackageValidationResult.Accepted());

                var stagingBlobService = new Mock<IStagingBlobService>(MockBehavior.Strict);
                stagingBlobService
                    .Setup(x => x.SavePackageFileAsync("PackageA", "1.0.0", It.IsAny<Stream>()))
                    .ReturnsAsync(new StagingFileReference("new.nupkg", "etag"));

                var stagedValidationMessageEmitter = new Mock<IStagedPackageValidationMessageEmitter>(MockBehavior.Strict);
                stagedValidationMessageEmitter
                    .Setup(x => x.StartValidationAsync(It.IsAny<StagedPackage>()))
                    .ReturnsAsync(StagedPackageStatus.Validating);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(owner))
                    .Returns(true);

                var target = new PackageStagingUploadService(
                    apiScopeEvaluator.Object,
                    featureFlagService.Object,
                    packageService.Object,
                    managementService.Object,
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingBlobService.Object,
                    stagedPackageRepository.Object,
                    stagingGroupRepository.Object,
                    stagedValidationMessageEmitter.Object);

                var isActiveNoOp = identical && (status == StagedPackageStatus.Validating || status == StagedPackageStatus.Ready);
                var result = await target.StagePackageAsync(
                    currentUser,
                    scopes,
                    Mock.Of<HttpContextBase>(),
                    packageFile,
                    assignGroup ? "release" : null,
                    listed: assignGroup ? false : (bool?)null);

                Assert.Equal(expectedStatusCode, result.StatusCode);
                Assert.Equal(assignGroup && expectedStatusCode == HttpStatusCode.OK ? (createGroup ? createdGroup.Key : requestedGroup.Key) : originalGroup.Key, stagedPackage.StagedPackageIdentity.StagingGroupKey);
                Assert.Equal(!assignGroup || expectedStatusCode == HttpStatusCode.Conflict, package.Listed);
                var createsSuccessor = expectedStatusCode == HttpStatusCode.OK && !isActiveNoOp;
                Assert.Equal(createsSuccessor || (assignGroup && isActiveNoOp) ? 1L : 0L, originalGroup.MutationRevision);
                Assert.Equal(assignGroup && !createGroup && expectedStatusCode == HttpStatusCode.OK ? 1L : 0L, requestedGroup.MutationRevision);
                stagingGroupRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagingGroup>()), createGroup ? Times.Once() : Times.Never());
                if (createGroup)
                {
                    Assert.Equal("release", createdGroup.Name);
                    Assert.Equal(owner.Key, createdGroup.OwnerKey);
                    Assert.Same(createdGroup, stagedPackage.StagedPackageIdentity.StagingGroup);
                }
                if (createsSuccessor && (status == StagedPackageStatus.Validating || status == StagedPackageStatus.Ready))
                {
                    Assert.Equal(StagedPackageStatus.Superseded, stagedPackage.Status);
                }
                else
                {
                    Assert.Equal(status, stagedPackage.Status);
                }

                if (createsSuccessor)
                {
                    stagedPackageRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagedPackage>()), Times.Once());
                    Assert.NotSame(stagedPackage, successor);
                    Assert.Equal("old.nupkg", stagedPackage.UploadedBlobPath);
                    Assert.Equal("old-etag", stagedPackage.UploadedBlobETag);
                    Assert.Equal("new.nupkg", successor.UploadedBlobPath);
                    Assert.Equal("etag", successor.UploadedBlobETag);
                    Assert.Equal(successor.Key, successor.StagedPackageIdentity.CurrentStagedPackageKey);
                    Assert.Same(successor, successor.StagedPackageIdentity.CurrentStagedPackage);
                }
                else
                {
                    stagedPackageRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagedPackage>()), Times.Never());
                }
            }

            [Fact]
            public async Task RejectsPromotingGroupBeforeStoringPackage()
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23, EmailAddress = "owner@example.com" };
                var scopes = new List<Scope>();
                var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
                apiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        scopes,
                        It.IsAny<IActionRequiringEntityPermissions<ActionOnNewPackageContext>>(),
                        It.IsAny<ActionOnNewPackageContext>(),
                        It.IsAny<string[]>()))
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>())).Returns(Task.CompletedTask);
                var managementService = new Mock<IPackageStagingManagementService>();
                var group = new StagingGroup { Key = 37, OwnerKey = owner.Key, ActivePromotionId = System.Guid.NewGuid() };
                managementService.Setup(x => x.FindStagingGroup(owner, "release")).Returns(group);
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService
                    .Setup(x => x.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);
                var blobService = new Mock<IStagingBlobService>();
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                var featureFlags = new Mock<IFeatureFlagService>();
                featureFlags.Setup(x => x.IsPackageStagingEnabled(owner)).Returns(true);
                var target = new PackageStagingUploadService(
                    apiScopeEvaluator.Object,
                    featureFlags.Object,
                    packageService.Object,
                    managementService.Object,
                    Mock.Of<IPackageUploadService>(),
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    blobService.Object,
                    stagedPackageRepository.Object,
                    Mock.Of<IEntityRepository<StagingGroup>>(),
                    Mock.Of<IStagedPackageValidationMessageEmitter>());
                using var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0");

                var result = await target.StagePackageAsync(currentUser, scopes, Mock.Of<HttpContextBase>(), packageFile, "release");

                Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
                blobService.Verify(x => x.SavePackageFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
                stagedPackageRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagedPackage>()), Times.Never);
            }

            [Fact]
            public async Task DoesNotCreateGroupWhenPackageValidationRejectsUpload()
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23, EmailAddress = "owner@example.com" };
                var scopes = new List<Scope>();
                var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
                apiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        scopes,
                        It.IsAny<IActionRequiringEntityPermissions<ActionOnNewPackageContext>>(),
                        It.IsAny<ActionOnNewPackageContext>(),
                        It.IsAny<string[]>()))
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>())).Returns(Task.CompletedTask);
                var managementService = new Mock<IPackageStagingManagementService>();
                managementService.Setup(x => x.FindStagingGroup(owner, "release")).Returns((StagingGroup)null);
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService
                    .Setup(x => x.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);
                var packageUploadService = new Mock<IPackageUploadService>();
                packageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), currentUser))
                    .ReturnsAsync(PackageValidationResult.Invalid("Rejected package."));
                var groups = new Mock<IEntityRepository<StagingGroup>>();
                var stagedPackages = new Mock<IEntityRepository<StagedPackage>>();
                var target = new PackageStagingUploadService(
                    apiScopeEvaluator.Object,
                    Mock.Of<IFeatureFlagService>(x => x.IsPackageStagingEnabled(owner) == true),
                    packageService.Object,
                    managementService.Object,
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    Mock.Of<IStagingBlobService>(),
                    stagedPackages.Object,
                    groups.Object,
                    Mock.Of<IStagedPackageValidationMessageEmitter>());
                using var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0");

                var result = await target.StagePackageAsync(currentUser, scopes, Mock.Of<HttpContextBase>(), packageFile, "release");

                Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
                Assert.Equal("Rejected package.", result.ErrorMessage);
                groups.Verify(x => x.InsertOnCommit(It.IsAny<StagingGroup>()), Times.Never);
                stagedPackages.Verify(x => x.InsertOnCommit(It.IsAny<StagedPackage>()), Times.Never);
            }
        }

        public class TheReplacePackageAsyncMethod
        {
            [Fact]
            public async Task RejectsDifferentPackageIdentity()
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23 };
                var stagedPackage = CreateStagedPackage(owner);
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);
                var target = CreateService(currentUser, packageService.Object, Mock.Of<IEntityRepository<StagedPackage>>());
                using var packageFile = TestPackage.CreateTestPackageStream("Different.Package", "1.0.0");

                var result = await target.ReplacePackageAsync(
                    currentUser,
                    Mock.Of<HttpContextBase>(),
                    stagedPackage,
                    packageFile);

                Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
                Assert.Equal("The replacement package identity does not match the staged package.", result.ErrorMessage);
            }

            [Fact]
            public async Task RejectsStaleAttempt()
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23 };
                var stagedPackage = CreateStagedPackage(owner);
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                stagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(Enumerable.Empty<StagedPackage>().AsQueryable());
                var target = CreateService(currentUser, packageService.Object, stagedPackageRepository.Object);
                using var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0");

                var result = await target.ReplacePackageAsync(
                    currentUser,
                    Mock.Of<HttpContextBase>(),
                    stagedPackage,
                    packageFile);

                Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
                Assert.Equal("The staged package was not found.", result.ErrorMessage);
            }

            [Fact]
            public async Task RejectsReplacementWhileGroupPromotionIsActive()
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23 };
                var stagedPackage = CreateStagedPackage(owner);
                var group = new StagingGroup
                {
                    Key = 37,
                    ActivePromotionId = System.Guid.NewGuid(),
                };
                stagedPackage.StagedPackageIdentity.StagingGroupKey = group.Key;
                stagedPackage.StagedPackageIdentity.StagingGroup = group;
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);
                packageService
                    .Setup(x => x.FindPackageRegistrationById("PackageA"))
                    .Returns(stagedPackage.StagedPackageIdentity.Package.PackageRegistration);
                packageService
                    .Setup(x => x.GetPackageStatus("PackageA", It.IsAny<NuGet.Versioning.NuGetVersion>()))
                    .Returns(PackageStatus.Staged);
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("PackageA", "1.0.0"))
                    .Returns(stagedPackage.StagedPackageIdentity.Package);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                stagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(new[] { stagedPackage }.AsQueryable());
                var target = CreateService(currentUser, packageService.Object, stagedPackageRepository.Object);
                using var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0");

                var result = await target.ReplacePackageAsync(
                    currentUser,
                    Mock.Of<HttpContextBase>(),
                    stagedPackage,
                    packageFile);

                Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
                Assert.Equal("The staged package cannot be replaced while its group is being promoted.", result.ErrorMessage);
            }

            private static StagedPackage CreateStagedPackage(User owner)
            {
                var package = new Package
                {
                    Key = 29,
                    NormalizedVersion = "1.0.0",
                    PackageRegistration = new PackageRegistration { Id = "PackageA" },
                    PackageStatusKey = PackageStatus.Staged,
                };
                var stagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = package.Key,
                    Package = package,
                    Owner = owner,
                    OwnerKey = owner.Key,
                };
                var stagedPackage = new StagedPackage
                {
                    Key = 31,
                    StagedPackageIdentityKey = stagedPackageIdentity.Key,
                    StagedPackageIdentity = stagedPackageIdentity,
                };
                stagedPackageIdentity.CurrentStagedPackageKey = stagedPackage.Key;
                stagedPackageIdentity.CurrentStagedPackage = stagedPackage;
                return stagedPackage;
            }

            private static PackageStagingUploadService CreateService(
                User currentUser,
                IPackageService packageService,
                IEntityRepository<StagedPackage> stagedPackageRepository)
            {
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService
                    .Setup(x => x.EvaluateUserPoliciesAsync(
                        SecurityPolicyAction.PackagePush,
                        currentUser,
                        It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);

                return new PackageStagingUploadService(
                    Mock.Of<IApiScopeEvaluator>(),
                    Mock.Of<IFeatureFlagService>(),
                    packageService,
                    Mock.Of<IPackageStagingManagementService>(),
                    Mock.Of<IPackageUploadService>(),
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    Mock.Of<IStagingBlobService>(),
                    stagedPackageRepository,
                    Mock.Of<IEntityRepository<StagingGroup>>(),
                    Mock.Of<IStagedPackageValidationMessageEmitter>());
            }
        }

    }
}
