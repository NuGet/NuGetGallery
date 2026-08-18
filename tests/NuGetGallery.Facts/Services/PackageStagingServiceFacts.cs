// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
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
    public class PackageStagingServiceFacts
    {
        public class TheStagePackageAsyncMethod
        {
            [Theory]
            [InlineData(true, StagedPackageStatus.Validating, 1)]
            [InlineData(false, StagedPackageStatus.Ready, 2)]
            public async Task StagesPackage(bool validationStarted, StagedPackageStatus expectedStatus, int expectedSaveCount)
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
                var stagedPackages = new Mock<DbSet<StagedPackage>>();
                stagedPackages
                    .Setup(x => x.Add(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(value => stagedPackage = value);

                var entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext.SetupGet(x => x.StagedPackages).Returns(stagedPackages.Object);
                entitiesContext.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
                var transaction = new Mock<IDbContextTransaction>();
                var database = new Mock<IDatabase>();
                database.Setup(x => x.BeginTransaction()).Returns(transaction.Object);
                entitiesContext.Setup(x => x.GetDatabase()).Returns(database.Object);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(owner))
                    .Returns(true);

                var validationMessageEmitter = new Mock<IValidationMessageEmitter<Package>>();
                validationMessageEmitter
                    .Setup(x => x.StartValidationAsync(package, It.IsAny<Guid>()))
                    .ReturnsAsync(validationStarted);

                var target = new PackageStagingService(
                    entitiesContext.Object,
                    apiScopeEvaluator.Object,
                    featureFlagService.Object,
                    packageService.Object,
                    packageUploadService.Object,
                    Mock.Of<IReservedNamespaceService>(),
                    securityPolicyService.Object,
                    stagingFiles.Object,
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
                transaction.Verify(x => x.Commit());
                entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Exactly(expectedSaveCount));
            }
        }

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

                var result = target.GetStagedPackagesForUser(currentUser);

                Assert.Equal(
                    new[] { "Organization.Package", "Personal.Package" },
                    result.Select(stagedPackage => stagedPackage.Package.PackageRegistration.Id));
                Assert.Equal(
                    new[] { "organization", "current" },
                    result.Select(stagedPackage => stagedPackage.Owner.Username));
            }

            private static PackageStagingService CreateService(
                IEnumerable<StagedPackage> stagedPackages,
                Func<User, bool> isEnabled)
            {
                var stagedPackagesList = stagedPackages.ToList();
                var stagedPackagesQuery = stagedPackagesList.AsQueryable();
                var stagedPackagesSet = new Mock<DbSet<StagedPackage>>();
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.Provider).Returns(stagedPackagesQuery.Provider);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.Expression).Returns(stagedPackagesQuery.Expression);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.ElementType).Returns(stagedPackagesQuery.ElementType);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.GetEnumerator()).Returns(() => stagedPackagesQuery.GetEnumerator());
                stagedPackagesSet.Setup(x => x.Include("Package.PackageRegistration")).Returns(stagedPackagesSet.Object);
                stagedPackagesSet.Setup(x => x.Include("Owner")).Returns(stagedPackagesSet.Object);
                var entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext
                    .SetupGet(x => x.StagedPackages)
                    .Returns(stagedPackagesSet.Object);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(It.IsAny<User>()))
                    .Returns((User owner) => isEnabled(owner));

                return new PackageStagingService(
                    entitiesContext.Object,
                    Mock.Of<IApiScopeEvaluator>(),
                    featureFlagService.Object,
                    Mock.Of<IPackageService>(),
                    Mock.Of<IPackageUploadService>(),
                    Mock.Of<IReservedNamespaceService>(),
                    Mock.Of<ISecurityPolicyService>(),
                    Mock.Of<IStagingBlobService>(),
                    Mock.Of<IValidationMessageEmitter<Package>>());
            }

            private static StagedPackage CreateStagedPackage(int packageKey, string id, string version, User owner)
            {
                var package = new Package
                {
                    Key = packageKey,
                    NormalizedVersion = version,
                    PackageRegistration = new PackageRegistration { Id = id },
                };

                return new StagedPackage
                {
                    PackageKey = packageKey,
                    Package = package,
                    OwnerKey = owner.Key,
                    Owner = owner,
                    UploadedDate = DateTime.UtcNow,
                };
            }
        }
    }
}
