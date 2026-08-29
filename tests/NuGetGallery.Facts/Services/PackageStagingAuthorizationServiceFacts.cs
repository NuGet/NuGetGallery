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
        public void ApiKeyCanManageMatchingEnabledOwner()
        {
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
            var stagedPackage = CreateStagedPackage(owner);
            var apiScopeEvaluator = new Mock<IApiScopeEvaluator>();
            apiScopeEvaluator
                .Setup(x => x.Evaluate(
                    owner,
                    It.IsAny<IEnumerable<Scope>>(),
                    It.IsAny<IActionRequiringEntityPermissions<PackageRegistration>>(),
                    stagedPackage.Package.PackageRegistration,
                    It.IsAny<string[]>()))
                .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(x => x.IsPackageStagingEnabled(owner))
                .Returns(true);
            var target = new PackageStagingAuthorizationService(
                apiScopeEvaluator.Object,
                featureFlagService.Object);

            var result = target.CanManageWithApiKey(owner, Array.Empty<Scope>(), stagedPackage);

            Assert.True(result);
        }

        private static StagedPackage CreateStagedPackage(User owner)
        {
            var registration = new PackageRegistration { Id = "PackageA" };
            registration.Owners.Add(owner);
            return new StagedPackage
            {
                Owner = owner,
                OwnerKey = owner.Key,
                Package = new Package
                {
                    PackageRegistration = registration,
                },
            };
        }
    }
}
