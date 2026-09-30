// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class SymbolPackageStagingUploadServiceFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReturnsBadRequestForInvalidSymbolMetadata(bool invalidFramework)
        {
            var currentUser = new User("uploader") { Key = 10 };
            var owner = new User("owner") { Key = 20, EmailAddress = "owner@example.test" };
            var scopes = new List<Scope>();
            var package = new Package
            {
                Key = 42,
                PackageRegistration = new PackageRegistration { Id = "Test.Package" },
                Version = "1.0.0",
                NormalizedVersion = "1.0.0",
                PackageStatusKey = PackageStatus.Available,
            };
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService.Setup(x => x.GetEnabledApiKeyOwner(currentUser, scopes)).Returns(owner);
            var contentObjectService = new Mock<IContentObjectService>();
            contentObjectService.Setup(x => x.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(currentUser)).Returns(true);
            var packageService = new Mock<IPackageService>();
            packageService.Setup(x => x.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0")).Returns(package);
            var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
            apiScopeEvaluator
                .Setup(x => x.Evaluate(currentUser, scopes, ActionsRequiringPermissions.UploadSymbolPackage, package.PackageRegistration, It.IsAny<string[]>()))
                .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
            var entitiesContext = new Mock<IEntitiesContext>();
            var identities = Enumerable.Empty<StagedPackageIdentity>().MockDbSet();
            identities.Setup(x => x.Include(It.IsAny<string>())).Returns(identities.Object);
            entitiesContext.Setup(x => x.StagedPackageIdentities).Returns(identities.Object);
            entitiesContext.Setup(x => x.SymbolPackages).Returns(Enumerable.Empty<SymbolPackage>().MockDbSet().Object);
            var symbolPackageService = new Mock<ISymbolPackageService>();
            Exception exception = new EntityException("Invalid symbol metadata.");
            if (invalidFramework)
            {
                exception = new FrameworkException("Invalid target framework.");
            }

            symbolPackageService.Setup(x => x.EnsureValidAsync(It.IsAny<PackageArchiveReader>())).ThrowsAsync(exception);
            var securityPolicyService = new Mock<ISecurityPolicyService>();
            securityPolicyService
                .Setup(x => x.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, It.IsAny<HttpContextBase>()))
                .ReturnsAsync(SecurityPolicyResult.SuccessResult);
            var stagingBlobService = new Mock<IStagingBlobService>(MockBehavior.Strict);
            var repository = new Mock<IEntityRepository<StagedSymbolPackage>>(MockBehavior.Strict);
            var validationMessageEmitter = new Mock<IStagedSymbolPackageValidationMessageEmitter>(MockBehavior.Strict);
            var target = new SymbolPackageStagingUploadService(
                apiScopeEvaluator.Object,
                contentObjectService.Object,
                entitiesContext.Object,
                packageService.Object,
                authorizationService.Object,
                symbolPackageService.Object,
                securityPolicyService.Object,
                stagingBlobService.Object,
                repository.Object,
                validationMessageEmitter.Object,
                Mock.Of<IPackageStagingManagementService>(),
                Mock.Of<IEntityRepository<StagingGroup>>());

            using var file = TestPackage.CreateTestSymbolPackageStream("Test.Package", "1.0.0");
            var result = await target.StageSymbolPackageAsync(currentUser, scopes, Mock.Of<HttpContextBase>(), file);

            Assert.False(result.Success);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal(exception.Message, result.ErrorMessage);
        }

        [Theory]
        [InlineData(PackageStatus.Available, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.Created)]
        [InlineData(PackageStatus.Staged, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.NotFound)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Validating, true, false, HttpStatusCode.Created)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.FailedValidation, true, false, HttpStatusCode.Created)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.Created)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, false, false, HttpStatusCode.NotFound)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Promoting, true, false, HttpStatusCode.Conflict)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, true, HttpStatusCode.Created)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, true, HttpStatusCode.Created)]
        [InlineData(PackageStatus.Available, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.Created, "release")]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.Created, "release")]
        [InlineData(PackageStatus.Available, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.Created, "new")]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, true, HttpStatusCode.Conflict, null, true)]
        [InlineData(PackageStatus.Available, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.Conflict, "release", true)]
        public async Task StagesSymbolsForAccessibleParent(
            PackageStatus parentStatus,
            bool hasStagedParent,
            StagedPackageStatus stagedStatus,
            bool sameOwner,
            bool grouped,
            HttpStatusCode expectedStatus,
            string groupId = null,
            bool promotionActive = false)
        {
            var currentUser = new User("uploader") { Key = 10 };
            var owner = new User("owner") { Key = 20, EmailAddress = "owner@example.test" };
            var scopes = new List<Scope>();
            var package = new Package
            {
                Key = 42,
                PackageRegistration = new PackageRegistration { Id = "Test.Package" },
                Version = "1.0.0",
                NormalizedVersion = "1.0.0",
                PackageStatusKey = parentStatus,
            };
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService.Setup(x => x.GetEnabledApiKeyOwner(currentUser, scopes)).Returns(owner);
            var contentObjectService = new Mock<IContentObjectService>();
            contentObjectService.Setup(x => x.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(currentUser)).Returns(true);
            var packageService = new Mock<IPackageService>();
            packageService.Setup(x => x.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0")).Returns(package);
            var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
            apiScopeEvaluator
                .Setup(x => x.Evaluate(currentUser, scopes, ActionsRequiringPermissions.UploadSymbolPackage, package.PackageRegistration, It.IsAny<string[]>()))
                .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
            var entitiesContext = new Mock<IEntitiesContext>();
            var identity = new StagedPackageIdentity { Key = package.Key, Package = package, Owner = owner, OwnerKey = sameOwner ? owner.Key : 999, CurrentStagedPackageKey = 50, StagingGroupKey = grouped ? 60 : (int?)null };
            identity.CurrentStagedPackage = new StagedPackage { Key = 50, StagedPackageIdentity = identity, Status = stagedStatus };
            var group = new StagingGroup { Key = 60, OwnerKey = owner.Key, Owner = owner, Id = "release" };
            if (promotionActive)
            {
                group.ActivePromotionId = Guid.NewGuid();
            }

            identity.StagingGroup = grouped ? group : null;
            var identities = (hasStagedParent ? new[] { identity } : Array.Empty<StagedPackageIdentity>()).MockDbSet();
            identities.Setup(x => x.Include(It.IsAny<string>())).Returns(identities.Object);
            entitiesContext.Setup(x => x.StagedPackageIdentities).Returns(identities.Object);
            entitiesContext.Setup(x => x.SymbolPackages).Returns(Enumerable.Empty<SymbolPackage>().MockDbSet().Object);
            var symbolPackageService = new Mock<ISymbolPackageService>();
            var symbolPackage = new SymbolPackage { Package = package, PackageKey = package.Key };
            symbolPackageService.Setup(x => x.EnsureValidAsync(It.IsAny<PackageArchiveReader>())).Returns(Task.CompletedTask);
            symbolPackageService.Setup(x => x.CreateSymbolPackage(package, It.IsAny<PackageStreamMetadata>())).Returns(symbolPackage);
            var securityPolicyService = new Mock<ISecurityPolicyService>();
            securityPolicyService
                .Setup(x => x.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, It.IsAny<HttpContextBase>()))
                .ReturnsAsync(SecurityPolicyResult.SuccessResult);
            var stagingBlobService = new Mock<IStagingBlobService>();
            stagingBlobService.Setup(x => x.SaveSymbolPackageFileAsync("Test.Package", "1.0.0", It.IsAny<System.IO.Stream>()))
                .ReturnsAsync(new StagingFileReference("test.package/1.0.0/file.snupkg", "etag"));
            StagedSymbolPackage attempt = null;
            var repository = new Mock<IEntityRepository<StagedSymbolPackage>>();
            repository.Setup(x => x.InsertOnCommit(It.IsAny<StagedSymbolPackage>())).Callback<StagedSymbolPackage>(value => attempt = value);
            repository.Setup(x => x.GetAll()).Returns(() => new[] { attempt }.AsQueryable());
            repository.Setup(x => x.CommitChangesAsync()).Returns(() =>
            {
                attempt.Key = 123;
                attempt.StagedPackageIdentityKey = package.Key;
                return Task.CompletedTask;
            });
            repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns<Func<Task>>(action => action());
            var validationMessageEmitter = new Mock<IStagedSymbolPackageValidationMessageEmitter>();
            validationMessageEmitter.Setup(x => x.StartValidationAsync(It.IsAny<StagedSymbolPackage>())).ReturnsAsync(StagedPackageStatus.Validating);
            var managementService = new Mock<IPackageStagingManagementService>();
            managementService.Setup(x => x.FindStagingGroup(owner, "release")).Returns(group);
            var groupRepository = new Mock<IEntityRepository<StagingGroup>>();
            var target = new SymbolPackageStagingUploadService(
                apiScopeEvaluator.Object,
                contentObjectService.Object,
                entitiesContext.Object,
                packageService.Object,
                authorizationService.Object,
                symbolPackageService.Object,
                securityPolicyService.Object,
                stagingBlobService.Object,
                repository.Object,
                validationMessageEmitter.Object,
                managementService.Object,
                groupRepository.Object);

            using var file = TestPackage.CreateTestSymbolPackageStream("Test.Package", "1.0.0");
            var result = await target.StageSymbolPackageAsync(currentUser, scopes, Mock.Of<HttpContextBase>(), file, groupId);

            Assert.Equal(expectedStatus, result.StatusCode);
            if (expectedStatus == HttpStatusCode.Created)
            {
                Assert.True(result.Success, result.ErrorMessage);
                Assert.Equal(PackageStatus.Staged, symbolPackage.StatusKey);
                Assert.Equal(package, attempt.StagedPackageIdentity.Package);
                Assert.Equal(owner, attempt.StagedPackageIdentity.Owner);
                Assert.Equal(attempt.Key, attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
                Assert.Equal("test.package/1.0.0/file.snupkg", attempt.UploadedBlobPath);
                if (hasStagedParent)
                {
                    Assert.Same(identity, attempt.StagedPackageIdentity);
                    Assert.Equal(parentStatus == PackageStatus.Staged ? 1 : 0, identity.CurrentStagedPackage.MutationRevision);
                }

                validationMessageEmitter.Verify(x => x.StartValidationAsync(attempt), Times.Once);
                if (grouped || groupId != null)
                {
                    Assert.Equal(groupId ?? group.Id, attempt.StagedPackageIdentity.StagingGroup.Id);
                    if (groupId == "new")
                    {
                        groupRepository.Verify(x => x.InsertOnCommit(It.Is<StagingGroup>(value => value.OwnerKey == owner.Key && value.Id == "new" && value.Name == "new")), Times.Once);
                    }
                    else
                    {
                        Assert.Equal(group.Key, attempt.StagedPackageIdentity.StagingGroupKey);
                        Assert.Equal(1, group.MutationRevision);
                    }
                }
                var status = target.GetStatus(currentUser, scopes, "Test.Package", "1.0.0");
                Assert.Equal("Test.Package", status.Id);
                Assert.Equal("1.0.0", status.Version);
                Assert.Equal(nameof(StagedPackageStatus.Validating), status.Status);
            }
            else
            {
                stagingBlobService.Verify(x => x.SaveSymbolPackageFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.Stream>()), Times.Never);
            }
        }
    }
}
