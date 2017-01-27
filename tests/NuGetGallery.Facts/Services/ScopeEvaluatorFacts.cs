// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class ScopeEvaluatorFacts
    {
        [Theory]
        [MemberData("ScopeClaimsAllowsActionForSubjectEvaluatesCorrectlyData")]
        public void ScopeClaimsAllowsActionForSubjectEvaluatesCorrectly(
            string scopeClaims, string subject, string[] requestedActions, bool expectedResult)
        {
            var result = ScopeEvaluator.ScopeClaimsAllowsActionForSubject(
                scopeClaims, 
                subject, 
                requestedActions);

            Assert.Equal(expectedResult, result);
        }

        private static string BuildScopeClaim(params Scope[] scopes)
        {
            return JsonConvert.SerializeObject(scopes);
        }

        public static IEnumerable<object[]> ScopeClaimsAllowsActionForSubjectEvaluatesCorrectlyData
        {
            get
            {
                return new[]
                {
                    // Push new package with legacy API key (no scopes)
                    new object[] {
                        string.Empty,
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        true
                    }, 

                    // Push new package with legacy API key (no scopes)
                    new object[] {
                        BuildScopeClaim(),
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        true
                    },

                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("SomePackage", NuGetScopes.PackagePush),
                            new Scope("SomePackage", NuGetScopes.PackagePush)),
                        null,
                        new[] { NuGetScopes.PackagePush },
                        true
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.PackagePush for the given package
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("SomePackage", NuGetScopes.PackagePush)),
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        true
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.All for all packages
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("*", NuGetScopes.All)),
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        true
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.List
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("*", NuGetScopes.PackageUnlist)),
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        false
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.List for all packages,
                    // and NuGetScopes.PackagePush for the given package
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("*", NuGetScopes.PackageUnlist), 
                            new Scope("SomePackage", NuGetScopes.PackagePush)),
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        true
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.List for the given package,
                    // and NuGetScopes.PackagePush for another package
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("SomePackage", NuGetScopes.PackageUnlist),
                            new Scope("SomeOtherPackage", NuGetScopes.All)),
                        "SomePackage",
                        new[] { NuGetScopes.PackagePush },
                        false
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.List for the given package,
                    // and NuGetScopes.All for another package, and no subject known
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("SomePackage", NuGetScopes.PackageUnlist),
                            new Scope("SomeOtherPackage", NuGetScopes.All)),
                        "",
                        new[] { NuGetScopes.PackagePush },
                        true
                    }, 

                    // Push new package with scoped API key which allows NuGetScopes.PackagePush for all packages,
                    // and no subject known
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("*", NuGetScopes.PackagePush)),
                        "",
                        new[] { NuGetScopes.PackagePush },
                        true
                    },

                    // Push package with a matching package pattern
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("Microsoft.*.Abstract", NuGetScopes.PackagePush)),
                        "Microsoft.Configuration.Abstract",
                        new [] { NuGetScopes.PackagePush },
                        true
                    },

                    // Push package with a non-matching package pattern
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("Microsoft.*.Abstract", NuGetScopes.PackagePush)),
                        "Microsoft.Configuration",
                        new [] { NuGetScopes.PackagePush },
                        false
                    },

                    // Push package when package pattern subject contains invalid characters 
                    new object[]
                    {
                        BuildScopeClaim(
                            new Scope("%@~!>^/\"*", NuGetScopes.PackagePush)),
                        "Microsoft.Configuration",
                        new [] { NuGetScopes.PackagePush },
                        false
                    }
                };
            }
        }
    }
}