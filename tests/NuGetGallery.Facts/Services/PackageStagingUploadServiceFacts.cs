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
            [InlineData(StagedPackageStatus.Validating)]
            [InlineData(StagedPackageStatus.Ready)]
            public async Task StagesPackage(StagedPackageStatus expectedStatus)
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
                var operations = new List<string>();
                var stagedPackageRepository = new Mock<IStagedPackageRepository>();
                stagedPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => stagedPackage = value);
                stagedPackageRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Callback(() =>
                    {
                        operations.Add("save");
                        stagedPackage.Key = 43;
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
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingFiles.Object,
                    stagedPackageRepository.Object,
                    stagedValidationMessageEmitter.Object);

                using (var packageFile = TestPackage.CreateTestPackageStream("PackageA", "1.0.0"))
                {
                    var result = await target.StagePackageAsync(
                        currentUser,
                        scopes,
                        Mock.Of<HttpContextBase>(),
                        packageFile);

                    Assert.True(result.Success, result.ErrorMessage);
                    Assert.Equal(HttpStatusCode.Created, result.StatusCode);
                }

                Assert.Equal(PackageStatus.Staged, package.PackageStatusKey);
                Assert.Equal(owner.Key, stagedPackage.OwnerKey);
                Assert.Equal(file.Path, stagedPackage.UploadedBlobPath);
                Assert.Equal(file.ETag, stagedPackage.UploadedBlobETag);
                Assert.Equal(streamMetadata.Hash, stagedPackage.UploadHash);
                Assert.Null(stagedPackage.ValidatedBlobPath);
                Assert.Null(stagedPackage.ValidatedBlobETag);
                Assert.Equal(expectedStatus, stagedPackage.Status);
                var expectedOperations = expectedStatus == StagedPackageStatus.Validating
                    ? new[] { "begin", "save", "enqueue", "commit" }
                    : new[] { "begin", "save", "enqueue", "save", "commit" };
                Assert.Equal(expectedOperations, operations);
                stagedValidationMessageEmitter.Verify(x => x.StartValidationAsync(stagedPackage), Times.Once);
                stagedPackageRepository.Verify(x => x.InsertOnCommit(stagedPackage), Times.Once);
                stagedPackageRepository.Verify(
                    x => x.CommitChangesAsync(),
                    expectedStatus == StagedPackageStatus.Validating ? Times.Once() : Times.Exactly(2));
                stagedPackageRepository.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<System.Func<Task>>()), Times.Once);
            }

            [Theory]
            [InlineData(StagedPackageStatus.Validating, HttpStatusCode.OK, true)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, true)]
            [InlineData(StagedPackageStatus.FailedValidation, HttpStatusCode.OK, true)]
            [InlineData(StagedPackageStatus.Superseded, HttpStatusCode.Conflict, true)]
            [InlineData(StagedPackageStatus.Deleted, HttpStatusCode.OK, true)]
            [InlineData(StagedPackageStatus.Validating, HttpStatusCode.OK, false)]
            [InlineData(StagedPackageStatus.Ready, HttpStatusCode.OK, false)]
            [InlineData(StagedPackageStatus.FailedValidation, HttpStatusCode.OK, false)]
            [InlineData(StagedPackageStatus.Superseded, HttpStatusCode.Conflict, false)]
            [InlineData(StagedPackageStatus.Deleted, HttpStatusCode.OK, false)]
            public async Task UploadReturnsExpectedStatus(StagedPackageStatus status, HttpStatusCode expectedStatusCode, bool identical)
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
                    PackageKey = package.Key,
                    Package = package,
                    OwnerKey = owner.Key,
                    Owner = owner,
                    UploadedBlobPath = "old.nupkg",
                    UploadedBlobETag = "old-etag",
                    UploadHash = uploadHash,
                    Status = status,
                };
                if (!identical)
                {
                    stagedPackage.UploadHash = "different";
                }

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

                var stagedPackageRepository = new Mock<IStagedPackageRepository>();
                StagedPackage successor = null;
                stagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(new[] { stagedPackage }.AsQueryable());
                stagedPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => successor = value);
                stagedPackageRepository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
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
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingBlobService.Object,
                    stagedPackageRepository.Object,
                    stagedValidationMessageEmitter.Object);

                var result = await target.StagePackageAsync(
                    currentUser,
                    scopes,
                    Mock.Of<HttpContextBase>(),
                    packageFile);

                Assert.Equal(expectedStatusCode, result.StatusCode);
                var isActiveNoOp = identical && (status == StagedPackageStatus.Validating || status == StagedPackageStatus.Ready);
                var createsSuccessor = expectedStatusCode == HttpStatusCode.OK && !isActiveNoOp;
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
                }
                else
                {
                    stagedPackageRepository.Verify(x => x.InsertOnCommit(It.IsAny<StagedPackage>()), Times.Never());
                }
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
                var target = CreateService(currentUser, packageService.Object, Mock.Of<IStagedPackageRepository>());
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
                var stagedPackageRepository = new Mock<IStagedPackageRepository>();
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

            private static StagedPackage CreateStagedPackage(User owner)
            {
                return new StagedPackage
                {
                    Key = 31,
                    PackageKey = 29,
                    Owner = owner,
                    OwnerKey = owner.Key,
                    Package = new Package
                    {
                        Key = 29,
                        NormalizedVersion = "1.0.0",
                        PackageRegistration = new PackageRegistration { Id = "PackageA" },
                        PackageStatusKey = PackageStatus.Staged,
                    },
                };
            }

            private static PackageStagingUploadService CreateService(
                User currentUser,
                IPackageService packageService,
                IStagedPackageRepository stagedPackageRepository)
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
                    Mock.Of<IPackageUploadService>(),
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    Mock.Of<IStagingBlobService>(),
                    stagedPackageRepository,
                    Mock.Of<IStagedPackageValidationMessageEmitter>());
            }
        }

    }
}
