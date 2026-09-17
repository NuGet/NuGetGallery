// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class PackageStagingAuthorizationServiceFacts
    {
        [Fact]
        public void OwnerCanManageWhenStagingIsEnabled()
        {
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
            var stagedPackage = CreateStagedPackage(owner);
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(owner))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                featureFlagService.Object);

            var result = target.CanManage(owner, stagedPackage);

            Assert.True(result);
        }

        [Fact]
        public void OwnerCannotManageWhenStagingIsDisabled()
        {
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                Mock.Of<IFeatureFlagService>());

            var result = target.CanManage(owner, CreateStagedPackage(owner));

            Assert.False(result);
        }

        [Fact]
        public void OrganizationMemberCanManageOrganizationAttempt()
        {
            var organization = new Organization("organization") { Key = 1 };
            var member = new User("member") { Key = 2, EmailAddress = "member@example.test" };
            var membership = new Membership
            {
                Organization = organization,
                Member = member,
            };
            organization.Members.Add(membership);
            member.Organizations.Add(membership);
            var stagedPackage = CreateStagedPackage(organization);
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(organization))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                featureFlagService.Object);

            var result = target.CanManage(member, stagedPackage);

            Assert.True(result);
        }

        [Fact]
        public void OtherPackageOwnerCannotManageInUi()
        {
            var stagedOwner = new User("stagedOwner") { Key = 1, EmailAddress = "staged@example.test" };
            var otherPackageOwner = new User("otherOwner") { Key = 2, EmailAddress = "other@example.test" };
            var stagedPackage = CreateStagedPackage(stagedOwner, otherPackageOwner);
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(stagedOwner))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                featureFlagService.Object);

            var result = target.CanManage(otherPackageOwner, stagedPackage);

            Assert.False(result);
        }

        [Fact]
        public void ApiKeyCanManageMatchingEnabledOwner()
        {
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
            var scopes = Array.Empty<Scope>();
            var stagedPackage = CreateStagedPackage(owner);
            var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
            apiScopeEvaluator
                .Setup(x => x.Evaluate(
                    owner,
                    scopes,
                    ActionsRequiringPermissions.ManageStagedPackage,
                    stagedPackage,
                    It.IsAny<string[]>()))
                .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(owner))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                apiScopeEvaluator.Object,
                featureFlagService.Object);

            var result = target.CanManageWithApiKey(owner, scopes, stagedPackage);

            Assert.True(result);
        }

        [Fact]
        public void ApiKeyCannotManageWhenStagingIsDisabled()
        {
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
            var scopes = Array.Empty<Scope>();
            var stagedPackage = CreateStagedPackage(owner);
            var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
            apiScopeEvaluator
                .Setup(x => x.Evaluate(
                    owner,
                    scopes,
                    ActionsRequiringPermissions.ManageStagedPackage,
                    stagedPackage,
                    It.IsAny<string[]>()))
                .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
            var target = new PackageStagingAuthorizationService(
                apiScopeEvaluator.Object,
                Mock.Of<IFeatureFlagService>());

            var result = target.CanManageWithApiKey(owner, scopes, stagedPackage);

            Assert.False(result);
        }

        [Fact]
        public void OtherPackageOwnerCannotManageWithApiKey()
        {
            var stagedOwner = new User("stagedOwner") { Key = 1, EmailAddress = "staged@example.test" };
            var otherPackageOwner = new User("otherOwner") { Key = 2, EmailAddress = "other@example.test" };
            var stagedPackage = CreateStagedPackage(stagedOwner, otherPackageOwner);
            var scopes = new[]
            {
                new Scope(ownerKey: null, subject: "PackageA", allowedAction: NuGetScopes.PackageStage),
            };
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(otherPackageOwner))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                new ApiScopeEvaluator(Mock.Of<IUserService>()),
                featureFlagService.Object);

            var result = target.CanManageWithApiKey(otherPackageOwner, scopes, stagedPackage);

            Assert.False(result);
        }

        [Fact]
        public void GetsEnabledOrganizationFromApiKeyOwnerScope()
        {
            var currentUser = new User("current") { Key = 1 };
            var organization = new Organization("organization") { Key = 2 };
            currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
            var scopes = new[] { new Scope(organization.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush) };
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(organization))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                featureFlagService.Object);

            var result = target.GetEnabledApiKeyOwner(currentUser, scopes);

            Assert.Same(organization, result);
        }

        [Fact]
        public void GetsEnabledOwnerByUsername()
        {
            var currentUser = new User("current") { Key = 1 };
            var organization = new Organization("organization") { Key = 2 };
            currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(organization))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                featureFlagService.Object);

            var result = target.GetEnabledOwner(currentUser, "ORGANIZATION");

            Assert.Same(organization, result);
        }

        [Fact]
        public void DoesNotGetApiKeyOwnerWhenScopesIdentifyMultipleOwners()
        {
            var currentUser = new User("current") { Key = 1 };
            var scopes = new[]
            {
                new Scope(currentUser.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush),
                new Scope(2, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush),
            };
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                Mock.Of<IFeatureFlagService>());

            var result = target.GetEnabledApiKeyOwner(currentUser, scopes);

            Assert.Null(result);
        }

        [Fact]
        public void DoesNotGetDisabledApiKeyOwner()
        {
            var currentUser = new User("current") { Key = 1 };
            var scopes = new[] { new Scope(currentUser.Key, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackagePush) };
            var target = new PackageStagingAuthorizationService(
                Mock.Of<IApiScopeEvaluator>(),
                Mock.Of<IFeatureFlagService>());

            var result = target.GetEnabledApiKeyOwner(currentUser, scopes);

            Assert.Null(result);
        }

        [Theory]
        [InlineData(NuGetScopes.PackagePush)]
        [InlineData(NuGetScopes.PackagePushVersion)]
        [InlineData(NuGetScopes.PackageUnlist)]
        public void OtherScopeCannotManageWithApiKey(string allowedAction)
        {
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
            var stagedPackage = CreateStagedPackage(owner);
            var scopes = new[]
            {
                new Scope(ownerKey: null, subject: "PackageA", allowedAction: allowedAction),
            };
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(owner))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                new ApiScopeEvaluator(Mock.Of<IUserService>()),
                featureFlagService.Object);

            var result = target.CanManageWithApiKey(owner, scopes, stagedPackage);

            Assert.False(result);
        }

        private static StagedPackage CreateStagedPackage(User owner, params User[] additionalPackageOwners)
        {
            var registration = new PackageRegistration { Id = "PackageA" };
            registration.Owners.Add(owner);
            foreach (var packageOwner in additionalPackageOwners)
            {
                registration.Owners.Add(packageOwner);
            }

            return new StagedPackage
            {
                StagedPackageIdentityKey = 42,
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = 42,
                    Package = new Package
                    {
                        Key = 42,
                        PackageRegistration = registration,
                    },
                    Owner = owner,
                    OwnerKey = owner.Key,
                },
            };
        }
    }
}
