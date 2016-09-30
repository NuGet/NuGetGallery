// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class ScopeEvaluatorFacts
    {
        [Theory]

        // Push new package with legacy API key (no scopes)
        [InlineData("", "SomePackage", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, true)]

        // Push new package with scoped API key which allows NuGetScopes.PackagePushNew for the given package
        [InlineData("SomePackage;package:pushnew", "SomePackage", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, true)]

        // Push new package with scoped API key which allows NuGetScopes.All for all packages
        [InlineData(";all", "SomePackage", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, true)]

        // Push new package with scoped API key which allows NuGetScopes.List
        [InlineData(";package:list", "SomePackage", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, false)]

        // Push new package with scoped API key which allows NuGetScopes.List for all packages,
        // and NuGetScopes.PackagePushNew for the given package
        [InlineData(";package:list|SomePackage;package:pushnew", "SomePackage", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, true)]

        // Push new package with scoped API key which allows NuGetScopes.List for the given package,
        // and NuGetScopes.PackagePushNew for another package
        [InlineData("SomePackage;package:list|;SomeOtherPackage;all", "SomePackage", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, false)]

        // Push new package with scoped API key which allows NuGetScopes.List for the given package,
        // and NuGetScopes.All for another package, and no subject known
        [InlineData("SomePackage;package:list|;SomeOtherPackage;all", "", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, false)]

        // Push new package with scoped API key which allows NuGetScopes.PackagePushNew for all packages,
        // and no subject known
        [InlineData(";package:pushnew", "", new[] { NuGetScopes.All, NuGetScopes.PackagePushNew }, true)]
        public void ScopeClaimsAllowsActionForSubjectEvaluatesCorrectly(
            string scopeClaims, string subject, string[] requestedActions, bool expectedResult)
        {
            var result = ScopeEvaluator.ScopeClaimsAllowsActionForSubject(
                scopeClaims, 
                subject, 
                requestedActions);

            Assert.Equal(expectedResult, result);
        }
    }
}