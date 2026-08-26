// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
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
            [InlineData(true, StagedPackageStatus.Validating)]
            [InlineData(false, StagedPackageStatus.Ready)]
            public async Task StagesPackage(bool validationStarted, StagedPackageStatus expectedStatus)
            {
                var currentUser = new User { Key = 17 };
                var owner = new User { Key = 23, EmailAddress = "owner@example.com" };
                var scopes = new List<Scope>();
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "PackageA" },
                    NormalizedVersion = "1.0.0",
                };
                var blobPath = "packagea/1.0.0/file.nupkg";
                var stagingFile = new StagingFileReference(
                    blobPath,
                    "\"etag\"",
                    3,
                    "hash");

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
                    .ReturnsAsync(stagingFile);

                StagedPackage stagedPackage = null;
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                stagedPackageRepository
                    .Setup(x => x.InsertOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => stagedPackage = value);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(owner))
                    .Returns(true);

                var validationMessageEmitter = new Mock<IValidationMessageEmitter<Package>>();
                var sequence = new MockSequence();
                validationMessageEmitter
                    .InSequence(sequence)
                    .Setup(x => x.StartValidationAsync(package, It.IsAny<Guid>()))
                    .ReturnsAsync(validationStarted);
                stagedPackageRepository
                    .InSequence(sequence)
                    .Setup(x => x.CommitChangesAsync())
                    .Returns(Task.CompletedTask);

                var target = new PackageStagingUploadService(
                    apiScopeEvaluator.Object,
                    featureFlagService.Object,
                    packageService.Object,
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingFiles.Object,
                    stagedPackageRepository.Object,
                    validationMessageEmitter.Object);

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
                Assert.Equal(blobPath, stagedPackage.BlobPath);
                Assert.Equal(stagingFile.ETag, stagedPackage.BlobETag);
                Assert.Equal(expectedStatus, stagedPackage.Status);
                validationMessageEmitter.Verify(x => x.StartValidationAsync(
                    package,
                    stagedPackage.ValidationTrackingId));
                stagedPackageRepository.Verify(x => x.InsertOnCommit(stagedPackage), Times.Once);
                stagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }
        }

    }
}
