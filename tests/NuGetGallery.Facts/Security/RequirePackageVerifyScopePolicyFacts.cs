// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Services.Security;
using Xunit;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;

namespace NuGetGallery.Security
{
    public class RequirePackageVerifyScopePolicyFacts
    {
        [Fact]
        public async Task Evaluate_ReturnsSuccessIfClaimHasPackageVerifyScope()
        {
            // Arrange and Act
            var scopes = "[{\"a\":\"package:verify\", \"s\":\"*\"}]";
            var result = await EvaluateAsync(scopes);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task Evaluate_ReturnsFailureIfEmptyScopeClaim()
        {
            // Arrange and Act
            var result = await EvaluateAsync(string.Empty);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Theory]
        [InlineData("[{\"a\":\"package:push\", \"s\":\"*\"}]")]
        [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"*\"}]")]
        public async Task Evaluate_ReturnsFailureIfClaimDoesNotHavePackageVerifyScope(string scopes)
        {
            // Arrange and Act
            var result = await EvaluateAsync(scopes);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        private Task<SecurityPolicyResult> EvaluateAsync(string scopes)
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

            var context = new UserSecurityPolicyEvaluationContext(
                new UserSecurityPolicy[] { new UserSecurityPolicy("RequireApiKeyWithPackageVerifyScopePolicy", "Subscription") },
                httpContext.Object);

            return new RequirePackageVerifyScopePolicy().EvaluateAsync(context);
        }
    }
}
