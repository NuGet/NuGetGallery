// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Security
{
    public class RequirePackageVerifyScopePolicyFacts
    {
        [Fact]
        public void EvaluateReturnsSuccessIfClaimHasPackageVerifyScope()
        {
            // Arrange and Act
            var scopes = "[{\"a\":\"package:verify\", \"s\":\"*\"}]";
            var result = Evaluate(scopes);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void EvaluateReturnsFailureIfEmptyScopeClaim()
        {
            // Arrange and Act
            var result = Evaluate(string.Empty);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Theory]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"*\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"*\"}]")]
        public void EvaluateReturnsFailureIfClaimHasDifferentScope(string scopes)
        {
            // Arrange and Act
            var result = Evaluate(scopes);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        private SecurityPolicyResult Evaluate(string scopes)
        {
            var identity = AuthenticationService.CreateIdentity(
                new User("testUser"),
                AuthenticationTypes.ApiKey,
                new Claim(NuGetClaims.ApiKey, string.Empty));

            if (!string.IsNullOrEmpty(scopes))
            {
                identity.AddClaim(new Claim(NuGetClaims.Scope, scopes));
            }

            var principal = new Mock<IPrincipal>();
            principal.Setup(p => p.Identity).Returns(identity);
            
            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.User).Returns(principal.Object);
            
            var context = new UserSecurityPolicyContext(httpContext.Object,
                new UserSecurityPolicy[] { new UserSecurityPolicy("RequireApiKeyWithPackageVerifyScopePolicy", "Enrollment") });

            return new RequirePackageVerifyScopePolicy().Evaluate(context);
        }
    }
}
