// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
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
    public class PackageStagingServiceFacts
    {
        public class TheStagePackageAsyncMethod
        {
            [Fact]
            public async Task StagesPackage()
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
                    .ReturnsAsync(blobPath);

                StagedPackage stagedPackage = null;
                var stagedPackages = new Mock<DbSet<StagedPackage>>();
                stagedPackages
                    .Setup(x => x.Add(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => stagedPackage = value);

                var entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext.SetupGet(x => x.StagedPackages).Returns(stagedPackages.Object);
                entitiesContext.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(owner))
                    .Returns(true);

                var target = new PackageStagingService(
                    entitiesContext.Object,
                    apiScopeEvaluator.Object,
                    featureFlagService.Object,
                    packageService.Object,
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingFiles.Object);

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
                entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            }
        }
    }
}
