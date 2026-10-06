// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class SymbolPackageStagingUploadServiceFacts
    {
        [Theory]
        [InlineData("Test.Package", true, true)]
        [InlineData("Other.Package", true, false)]
        [InlineData("Test.Package", false, false)]
        public void SymbolStatusAfterOwnershipLossStillRequiresStagingOwnershipAndMatchingScope(string subject, bool ownsStaging, bool allowed)
        {
            var owner = new User("owner") { Key = 20, EmailAddress = "owner@example.test" };
            var package = new Package { Key = 42, NormalizedVersion = "1.0.0", PackageRegistration = new PackageRegistration { Id = "Test.Package" } };
            var identity = new StagedPackageIdentity { Key = package.Key, Package = package, Owner = owner, OwnerKey = ownsStaging ? owner.Key : 21, CurrentStagedSymbolPackageKey = 71 };
            var attempt = new StagedSymbolPackage { Key = 71, StagedPackageIdentity = identity, StagedPackageIdentityKey = identity.Key, Status = StagedPackageStatus.Ready };
            var scopes = new[] { new Scope(owner.Key, subject, NuGetScopes.PackageStage) };
            var authorization = Mock.Of<IPackageStagingAuthorizationService>(service => service.GetEnabledApiKeyOwner(owner, scopes) == owner);
            var packages = Mock.Of<IPackageService>(service => service.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0") == package);
            var attempts = Mock.Of<IEntityRepository<StagedSymbolPackage>>(repository => repository.GetAll() == new[] { attempt }.AsQueryable());
            var target = new SymbolPackageStagingUploadService(
                Mock.Of<IApiScopeEvaluator>(), Mock.Of<IContentObjectService>(), Mock.Of<IEntitiesContext>(), packages, authorization,
                Mock.Of<ISymbolPackageService>(), Mock.Of<ISecurityPolicyService>(), Mock.Of<IStagingBlobService>(), attempts,
                Mock.Of<IStagedSymbolPackageValidationMessageEmitter>(), Mock.Of<IPackageStagingManagementService>(),
                Mock.Of<IEntityRepository<StagingGroup>>(), new Configuration.AppConfiguration(), Mock.Of<IStagingQuotaService>(), Mock.Of<IMessageService>());

            var result = target.GetStatus(owner, scopes, "Test.Package", "1.0.0");

            Assert.Equal(allowed, result != null);
            if (allowed)
            {
                Assert.Equal("Ready", result.Status);
                var expirationDate = result.Expires;
                package.PackageRegistration.Owners.Add(owner);
                Assert.Equal(expirationDate, target.GetStatus(owner, scopes, "Test.Package", "1.0.0").Expires);
            }
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true, false)]
        public async Task UiReplacementRejectsStaleUnauthorizedOrUnownedAttempt(bool current, bool authorized, bool ownsRegistration = true)
        {
            var owner = new User("owner") { Key = 20 };
            var package = new Package
            {
                Key = 42,
                PackageRegistration = new PackageRegistration { Id = "Test.Package" },
                NormalizedVersion = "1.0.0",
                PackageStatusKey = PackageStatus.Available,
            };
            if (ownsRegistration)
            {
                package.PackageRegistration.Owners.Add(owner);
            }

            var identity = new StagedPackageIdentity { Key = package.Key, Package = package, Owner = owner, OwnerKey = owner.Key, CurrentStagedSymbolPackageKey = current ? 71 : 72 };
            var attempt = new StagedSymbolPackage { Key = 71, StagedPackageIdentity = identity };
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService.Setup(x => x.CanManage(owner, attempt)).Returns(authorized);
            var contentObjectService = new Mock<IContentObjectService>();
            contentObjectService.Setup(x => x.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(owner)).Returns(true);
            var packageService = new Mock<IPackageService>();
            packageService.Setup(x => x.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0")).Returns(package);
            var identities = new[] { identity }.MockDbSet();
            identities.Setup(x => x.Include(It.IsAny<string>())).Returns(identities.Object);
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext.Setup(x => x.StagedPackageIdentities).Returns(identities.Object);
            var target = new SymbolPackageStagingUploadService(
                Mock.Of<IApiScopeEvaluator>(),
                contentObjectService.Object,
                entitiesContext.Object,
                packageService.Object,
                authorizationService.Object,
                new Mock<ISymbolPackageService>(MockBehavior.Strict).Object,
                new Mock<ISecurityPolicyService>(MockBehavior.Strict).Object,
                new Mock<IStagingBlobService>(MockBehavior.Strict).Object,
                new Mock<IEntityRepository<StagedSymbolPackage>>(MockBehavior.Strict).Object,
                new Mock<IStagedSymbolPackageValidationMessageEmitter>(MockBehavior.Strict).Object,
                Mock.Of<IPackageStagingManagementService>(),
                Mock.Of<IEntityRepository<StagingGroup>>(),
                new Configuration.AppConfiguration { SiteRoot = "https://gallery.test/" },
                Mock.Of<IStagingQuotaService>(),
                Mock.Of<IMessageService>());
            using var file = TestPackage.CreateTestSymbolPackageStream("Test.Package", "1.0.0");

            var result = await target.ReplaceSymbolPackageAsync(owner, Mock.Of<HttpContextBase>(), attempt, file);

            Assert.Equal(ownsRegistration ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, result.StatusCode);
            if (!ownsRegistration)
            {
                Assert.Equal(StagingOwnershipPolicy.BlockerMessage, result.ErrorMessage);
            }
            Assert.Equal(current ? 71 : 72, identity.CurrentStagedSymbolPackageKey);
        }

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
                Mock.Of<IEntityRepository<StagingGroup>>(),
                new Configuration.AppConfiguration { SiteRoot = "https://gallery.test/" },
                Mock.Of<IStagingQuotaService>(),
                Mock.Of<IMessageService>());

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
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK, null, false, StagedPackageStatus.Validating)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.Conflict, null, false, StagedPackageStatus.Promoting)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, true, HttpStatusCode.OK, null, false, StagedPackageStatus.Ready, true)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.BadRequest, null, false, StagedPackageStatus.Ready, true, "Other.Package")]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK, null, false, StagedPackageStatus.Validating, false, "Test.Package", true)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, true, HttpStatusCode.OK, null, false, StagedPackageStatus.Ready, true, "Test.Package", true)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK, null, false, StagedPackageStatus.FailedValidation, false, "Test.Package", true)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, true, HttpStatusCode.OK, "new", false, StagedPackageStatus.Ready, false, "Test.Package", true)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK, "release", false, StagedPackageStatus.Ready, false, "Test.Package", true)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK, "new", false, StagedPackageStatus.Ready, false, "Test.Package", true)]
        [InlineData(PackageStatus.Deleted, true, StagedPackageStatus.Deleted, true, true, HttpStatusCode.OK, null, false, StagedPackageStatus.WaitingForParent)]
        [InlineData(PackageStatus.Deleted, true, StagedPackageStatus.Deleted, true, false, HttpStatusCode.OK, null, false, StagedPackageStatus.WaitingForParent, false, "Test.Package", true)]
        [InlineData(PackageStatus.Deleted, true, StagedPackageStatus.Deleted, true, false, HttpStatusCode.NotFound)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.Conflict, null, false, StagedPackageStatus.Ready, false, "Test.Package", false, true)]
        [InlineData(PackageStatus.Available, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.Conflict, null, false, null, false, "Test.Package", false, false, true)]
        [InlineData(PackageStatus.Available, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK, null, false, StagedPackageStatus.Ready, true, "Test.Package", false, false, true)]
        [InlineData(PackageStatus.Available, false, StagedPackageStatus.Ready, true, false, HttpStatusCode.Created,
            null, false, null, false, "Test.Package", false, false, false, StagedPackageStatus.Ready)]
        [InlineData(PackageStatus.Staged, true, StagedPackageStatus.Ready, true, false, HttpStatusCode.OK,
            null, false, StagedPackageStatus.Ready, true, "Test.Package", false, false, false, StagedPackageStatus.Ready)]
        public async Task StagesSymbolsForAccessibleParent(
            PackageStatus parentStatus,
            bool hasStagedParent,
            StagedPackageStatus stagedStatus,
            bool sameOwner,
            bool grouped,
            HttpStatusCode expectedStatus,
            string groupId = null,
            bool promotionActive = false,
            StagedPackageStatus? previousStatus = null,
            bool uiReplacement = false,
            string replacementId = "Test.Package",
            bool identical = false,
            bool expired = false,
            bool quotaReached = false,
            StagedPackageStatus validationStatus = StagedPackageStatus.Validating)
        {
            var currentUser = new User("uploader") { Key = 10 };
            var owner = new User("owner") { Key = 20, EmailAddress = "owner@example.test" };
            var scopes = new List<Scope> { new Scope(owner.Key, "Test.Package", NuGetScopes.PackageStage) };
            var package = new Package
            {
                Key = 42,
                PackageRegistration = new PackageRegistration { Id = "Test.Package" },
                Version = "1.0.0",
                NormalizedVersion = "1.0.0",
                PackageStatusKey = parentStatus,
            };
            package.PackageRegistration.Owners.Add(owner);
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
            var previousSymbol = new SymbolPackage { Key = 70, Package = package, PackageKey = package.Key, StatusKey = PackageStatus.Staged, Hash = "old-hash" };
            var previousAttempt = new StagedSymbolPackage
            {
                Key = 71,
                StagedPackageIdentity = identity,
                SymbolPackage = previousSymbol,
                Status = previousStatus ?? StagedPackageStatus.Ready,
                UploadedBlobPath = "previous.snupkg",
                UploadedBlobETag = "previous-etag",
            };
            if (previousStatus.HasValue)
            {
                identity.CurrentStagedSymbolPackageKey = previousAttempt.Key;
                identity.CurrentStagedSymbolPackage = previousAttempt;
            }
            authorizationService.Setup(x => x.CanManage(currentUser, previousAttempt)).Returns(true);
            var previousDeadline = DateTime.UtcNow.AddDays(expired ? -1 : 1);
            previousAttempt.ExpirationDate = previousDeadline;
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
                if (identical && attempt == null && groupId != null)
                {
                    Assert.Equal(1, previousAttempt.MutationRevision);
                }

                if (attempt != null)
                {
                    attempt.Key = 123;
                    attempt.StagedPackageIdentityKey = package.Key;
                }
                return Task.CompletedTask;
            });
            var committed = false;
            repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns<Func<Task>>(async action =>
            {
                await action();
                committed = true;
            });
            var validationMessageEmitter = new Mock<IStagedSymbolPackageValidationMessageEmitter>();
            validationMessageEmitter.Setup(x => x.StartValidationAsync(It.IsAny<StagedSymbolPackage>())).ReturnsAsync(validationStatus);
            var managementService = new Mock<IPackageStagingManagementService>();
            managementService.Setup(x => x.FindStagingGroup(owner, "release")).Returns(group);
            var groupRepository = new Mock<IEntityRepository<StagingGroup>>();
            var quota = new Mock<IStagingQuotaService>();
            quota.Setup(service => service.EnsureCapacityAsync(owner)).Returns(Task.CompletedTask);
            if (quotaReached)
            {
                quota.Setup(service => service.EnsureCapacityAsync(owner)).ThrowsAsync(new StagingQuotaExceededException());
            }
            var messages = new Mock<IMessageService>();
            messages.Setup(service => service.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Callback(() => Assert.True(committed))
                .Returns(Task.CompletedTask);
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
                groupRepository.Object,
                new Configuration.AppConfiguration { SiteRoot = "https://gallery.test/", GalleryOwner = new MailAddress("support@gallery.test", "Gallery") },
                quota.Object,
                messages.Object);

            using var file = TestPackage.CreateTestSymbolPackageStream(replacementId, "1.0.0");
            if (identical)
            {
                previousSymbol.Hash = CryptographyService.GenerateHash(file, CoreConstants.Sha512HashAlgorithmId);
                file.Position = 0;
            }
            PackageStagingResult result;
            if (uiReplacement)
            {
                result = await target.ReplaceSymbolPackageAsync(currentUser, Mock.Of<HttpContextBase>(), previousAttempt, file);
            }
            else
            {
                result = await target.StageSymbolPackageAsync(currentUser, scopes, Mock.Of<HttpContextBase>(), file, groupId);
            }

            Assert.Equal(expectedStatus, result.StatusCode);
            var stagingUrl = "https://gallery.test/account/staging/symbols/Test.Package/1.0.0/manage";
            messages.Verify(service => service.SendMessageAsync(
                It.Is<StagedPackageUploadedMessage>(message => message.GetBody(EmailFormat.Markdown).Contains(stagingUrl)),
                It.IsAny<bool>(), It.IsAny<bool>()),
                attempt == null ? Times.Never() : Times.Once());
            messages.Verify(service => service.SendMessageAsync(
                It.Is<StagedPackageValidationSucceededMessage>(message => message.GetBody(EmailFormat.Markdown).Contains(stagingUrl)),
                It.IsAny<bool>(), It.IsAny<bool>()),
                attempt?.Status == StagedPackageStatus.Ready ? Times.Once() : Times.Never());
            if (quotaReached && expectedStatus == HttpStatusCode.Conflict)
            {
                quota.Verify(service => service.EnsureCapacityAsync(owner), Times.Once);
                Assert.Null(attempt);
                repository.Verify(service => service.CommitChangesAsync(), Times.Never);
                stagingBlobService.Verify(service => service.SaveSymbolPackageFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.Stream>()), Times.Never);
                return;
            }
            if (quotaReached && expectedStatus == HttpStatusCode.OK)
            {
                quota.Verify(service => service.EnsureCapacityAsync(It.IsAny<User>()), Times.Never);
            }
            securityPolicyService.Verify(x => x.EvaluateUserPoliciesAsync(SecurityPolicyAction.PackagePush, currentUser, It.IsAny<HttpContextBase>()), uiReplacement ? Times.Never() : Times.Once());
            if (expired)
            {
                Assert.Equal(previousDeadline, previousAttempt.ExpirationDate);
                repository.Verify(x => x.InsertOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
                stagingBlobService.Verify(x => x.SaveSymbolPackageFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.Stream>()), Times.Never);
            }
            else if (identical && previousStatus != StagedPackageStatus.FailedValidation)
            {
                Assert.Same(previousAttempt, result.StagedSymbolPackage);
                Assert.Equal(previousAttempt.Key, identity.CurrentStagedSymbolPackageKey);
                Assert.Equal(previousDeadline, previousAttempt.ExpirationDate);
                Assert.Equal(previousStatus, previousAttempt.Status);
                Assert.Equal(groupId ?? (grouped ? group.Id : null), identity.StagingGroup?.Id);
                var reassigned = groupId != null && (!grouped || groupId != group.Id);
                Assert.Equal(reassigned ? 1 : 0, previousAttempt.MutationRevision);
                Assert.Equal(reassigned && parentStatus == PackageStatus.Staged ? 1 : 0, identity.CurrentStagedPackage.MutationRevision);
                repository.Verify(x => x.CommitChangesAsync(), reassigned ? Times.Once() : Times.Never());
                stagingBlobService.Verify(x => x.SaveSymbolPackageFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.Stream>()), Times.Never);
                repository.Verify(x => x.InsertOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
                validationMessageEmitter.Verify(x => x.StartValidationAsync(It.IsAny<StagedSymbolPackage>()), Times.Never);
            }
            else if (expectedStatus == HttpStatusCode.Created || expectedStatus == HttpStatusCode.OK)
            {
                Assert.True(result.Success, result.ErrorMessage);
                Assert.Same(attempt, result.StagedSymbolPackage);
                Assert.Equal(PackageStatus.Staged, symbolPackage.StatusKey);
                Assert.Equal(package, attempt.StagedPackageIdentity.Package);
                Assert.Equal(owner, attempt.StagedPackageIdentity.Owner);
                Assert.Equal(attempt.Key, attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
                Assert.Equal("test.package/1.0.0/file.snupkg", attempt.UploadedBlobPath);
                if (previousStatus.HasValue)
                {
                    Assert.NotEqual(previousAttempt.Key, attempt.Key);
                    Assert.Equal(StagedPackageStatus.Superseded, previousAttempt.Status);
                    Assert.Equal("previous.snupkg", previousAttempt.UploadedBlobPath);
                    Assert.Equal("previous-etag", previousAttempt.UploadedBlobETag);
                    if (!identical)
                    {
                        Assert.Equal("old-hash", previousSymbol.Hash);
                    }
                    Assert.Equal(PackageStatus.Staged, previousSymbol.StatusKey);
                    Assert.Equal(parentStatus == PackageStatus.Deleted ? StagedPackageStatus.WaitingForParent : validationStatus, attempt.Status);
                    Assert.Same(previousAttempt.StagedPackageIdentity, attempt.StagedPackageIdentity);
                    repository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
                }
                if (hasStagedParent)
                {
                    Assert.Same(identity, attempt.StagedPackageIdentity);
                    Assert.Equal(parentStatus == PackageStatus.Staged ? 1 : 0, identity.CurrentStagedPackage.MutationRevision);
                }

                validationMessageEmitter.Verify(x => x.StartValidationAsync(attempt), parentStatus == PackageStatus.Deleted ? Times.Never() : Times.Once());
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
                Assert.Equal(parentStatus == PackageStatus.Deleted ? nameof(StagedPackageStatus.WaitingForParent) : validationStatus.ToString(), status.Status);
            }
            else
            {
                stagingBlobService.Verify(x => x.SaveSymbolPackageFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.Stream>()), Times.Never);
                if (previousStatus.HasValue)
                {
                    Assert.Equal(previousStatus.Value, previousAttempt.Status);
                    Assert.Equal(previousAttempt.Key, identity.CurrentStagedSymbolPackageKey);
                }
            }
        }
    }
}
