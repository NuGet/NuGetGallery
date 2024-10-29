// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using Xunit;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;

namespace NuGetGallery.Extensions
{
    public class PrincipalExtensionsFacts
    {
        public class TheGetClaimOrDefaultMethod
        {
            [Fact]
            public void WhenSelfIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.GetClaimOrDefault(null, NuGetClaims.ApiKey);
                });
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void WhenClaimTypeIsEmptyOrNull_ThrowsArgumentNullException(string claimType)
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.GetClaimOrDefault(new ClaimsPrincipal(), claimType);
                });
            }

            [Fact]
            public void WhenClaimFound_ReturnsClaim()
            {
                var principal = Fakes.ToPrincipal(new User("user"));

                Assert.Equal("user", principal.GetClaimOrDefault(ClaimsIdentity.DefaultNameClaimType));
            }

            [Fact]
            public void WhenClaimNotFound_ReturnsNull()
            {
                var principal = Fakes.ToPrincipal(new User("user"));

                Assert.Null(principal.GetClaimOrDefault("noSuchClaim"));
            }
        }

        public class TheGetAuthenticationTypeMethod
        {
            [Fact]
            public void WhenSelfIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.GetAuthenticationType(null);
                });
            }

            [Fact]
            public void WhenNotClaimsIdentity_ReturnsEmpty()
            {
                var identity = Fakes.ToIdentity(new User("user"));

                Assert.Equal("", identity.AuthenticationType);
            }

            [Fact]
            public void ReturnsAuthenticationType()
            {
                var principal = Fakes.ToPrincipal(new User("user"));

                Assert.Equal("Test", principal.Identity.AuthenticationType);
            }
        }

        public class TheGetScopesFromClaimMethod
        {
            [Fact]
            public void WhenSelfIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.GetScopesFromClaim(null);
                });
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
            [InlineData("[{\"o\":\"1234\", \"a\":\"package:unlist\", \"s\":\"theId\"}]")]
            public void WhenScopeClaims_Returns(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                var scopes = identity.GetScopesFromClaim();

                Assert.Single(scopes);
                Assert.Equal("theId", scopes.First().Subject);
            }

            [Fact]
            public void WhenNoScopeClaims_ReturnsNull()
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty));

                Assert.Null(identity.GetScopesFromClaim());
            }

            [Theory]
            [InlineData("")]
            [InlineData(" ")]
            [InlineData("[]")]
            public void WhenEmptyScopeClaim_ReturnsNull(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                Assert.Null(identity.GetScopesFromClaim());
            }
        }

        public class TheIsAdministratorMethod
        {
            [Fact]
            public void WhenSelfIsNull_ReturnsFalse()
            {
                Assert.False(PrincipalExtensions.IsAdministrator(null));
            }

            [Fact]
            public void WhenAdminRoleClaim_ReturnsTrue()
            {
                var user = new User("admin")
                {
                    Roles = new [] { new Role { Key = 1, Name = "Admins" } }
                };
                var principal = Fakes.ToPrincipal(user);

                Assert.True(PrincipalExtensions.IsAdministrator(principal));
            }

            [Fact]
            public void WhenNoAdminRoleClaim_ReturnsFalse()
            {
                var user = new User("admin");
                var principal = Fakes.ToPrincipal(user);

                Assert.False(PrincipalExtensions.IsAdministrator(principal));
            }
        }

        public class TheIsScopedAuthenticationMethod
        {
            [Fact]
            public void WhenSelfIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.IsScopedAuthentication(null);
                });
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
            [InlineData("[{\"o\":\"1234\", \"a\":\"package:unlist\", \"s\":\"theId\"}]")]
            public void WhenScopeClaim_ReturnsTrue(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                Assert.True(identity.IsScopedAuthentication());
            }

            [Fact]
            public void WhenNoScopeClaim_ReturnsTrue()
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty));

                Assert.False(identity.IsScopedAuthentication());
            }
        }

        public class TheHasExplicitScopeActionMethod
        {
            [Fact]
            public void WhenSelfIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.HasExplicitScopeAction(null, "action");
                });
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
            [InlineData("[{\"o\":\"1234\", \"a\":\"package:push\", \"s\":\"theId\"}]")]
            public void WhenScopeClaimsWithDifferentAction_ReturnsFalse(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                Assert.False(identity.HasExplicitScopeAction(NuGetScopes.PackageVerify));
            }

            [Theory]
            [InlineData("[{\"a\":\"package:verify\", \"s\":\"theId\"}]")]
            [InlineData("[{\"o\":\"1234\", \"a\":\"package:verify\", \"s\":\"theId\"}]")]
            public void WhenScopeClaimsWithSameAction_ReturnsTrue(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                Assert.True(identity.HasExplicitScopeAction(NuGetScopes.PackageVerify));
            }

            [Fact]
            public void WhenNoScopeClaims_ReturnsFalse()
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty));

                Assert.False(identity.HasExplicitScopeAction(NuGetScopes.PackageVerify));
            }
        }

        public class TheHasScopeThatAllowsActionsMethod
        {
            [Fact]
            public void WhenSelfIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    PrincipalExtensions.HasScopeThatAllowsActions(null, "action");
                });
            }

            [Fact]
            public void WhenHasNoScopes_ReturnsFalse()
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty));

                Assert.False(identity.HasExplicitScopeAction(NuGetScopes.PackagePush));
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]")]
            [InlineData("[{\"o\":\"1234\", \"a\":\"package:push\", \"s\":\"theId\"}]")]
            public void WhenHasExplicitScope_ReturnsTrue(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                Assert.True(identity.HasExplicitScopeAction(NuGetScopes.PackagePush));
            }

            [Theory]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
            [InlineData("[{\"o\":\"1234\", \"a\":\"package:pushversion\", \"s\":\"theId\"}]")]
            public void WhenHasDifferentScope_ReturnsFalse(string scopeClaim)
            {
                var identity = AuthenticationService.CreateIdentity(
                    new User("user"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopeClaim));

                Assert.False(identity.HasExplicitScopeAction(NuGetScopes.PackagePush));
            }
        }
    }
}
