// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Services.Authentication
{
    public class TokenTestHelper
    {
        public static readonly Dictionary<string, object> ValidClaims = new()
        {
            { "repository_owner", "test-owner" },
            { "repository_owner_id", "id-123" },
            { "repository", "test-owner/test-repo" },
            { "repository_id", "id-456" },
            { "job_workflow_ref", "test-owner/test-repo/.github/workflows/test.yml@refs/heads/main" },
            { "environment", "production" },
            { "jti", "test-token-id" }
        };

        public const string PermanentPolicyCriteria = """
            {
                "owner": "test-owner",
                "ownerId": "id-123",
                "repository": "test-repo",
                "repositoryId": "id-456",
                "workflow": "test.yml",
                "environment": "production"
            }
            """;

        public const string PermanentPolicyCriteriaNoEnvironment = """
            {
                "owner": "test-owner",
                "ownerId": "id-123",
                "repository": "test-repo",
                "repositoryId": "id-456",
                "workflow": "test.yml"
            }
            """;

        public const string TempotatyPolicyCriteria = """
            {
                "owner": "test-owner",
                "repository": "test-repo",
                "workflow": "test.yml",
                "environment": "production",
                "validateBy": "2222-01-01T00:00:00Z"
            }
            """;

        public static readonly string ExpiredPolicyCriteria = """
            {
                "jti": "test-jti",
                "owner": "test-owner",
                "repository": "test-repo",
                "workflow": "test.yml",
                "environment": "production",
                "validateBy": "1999-01-01T00:00:00Z"
            }
            """;

        public static readonly string ValidIssuer = GitHubTokenPolicyValidator.Issuer;
        public static readonly string InvalidIssuer = "invalid-issuer";

        public static readonly string ValidAudience = "nuget.org";
        public static readonly string InvalidAudience = "invalid-audience";

        public static readonly SymmetricSecurityKey DefaultSigningKey = CreateTestSymmetricKey();

        public static JsonWebToken CreateTestJwt()
            => CreateTestJwt(ValidClaims, ValidIssuer, ValidAudience, DefaultSigningKey);

        public static JsonWebToken CreateTestJwtWithCustomClaimValue(string claimName, string? value = null)
        {
            var claims = new Dictionary<string, object>(ValidClaims);
            if (value == null)
            {
                claims.Remove(claimName);
            }
            else
            {
                claims[claimName] = value;
            }
            return CreateTestJwt(claims, ValidIssuer, ValidAudience, DefaultSigningKey);
        }

        public static JsonWebToken CreateTestJwt(Dictionary<string, object> claims, string issuer, string audience, SecurityKey signingKey)
        {
            var handler = new JsonWebTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Claims = claims,
                Issuer = issuer,
                Audience = audience,
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = CreateTestSigningCredentials(signingKey)
            };

            var tokenString = handler.CreateToken(tokenDescriptor);
            return handler.ReadJsonWebToken(tokenString);
        }

        private static SigningCredentials CreateTestSigningCredentials(SecurityKey? signingKey = null)
        {
            var key = signingKey ?? new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-256-bit-secret-key-here-32-chars"));
            return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        }

        public static SymmetricSecurityKey CreateTestSymmetricKey(string keyMaterial = "your-256-bit-secret-key-here-32-chars")
        {
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyMaterial));
        }
    }

    public class GitHubTokenPolicyValidatorFacts
    {
        public class TheValidateTokenAsyncMethod : GitHubTokenPolicyValidatorFacts
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WithToken_DoesNotThrow(bool isTokenValid)
            {
                // Arrange
                var token = TokenTestHelper.CreateTestJwt();
                JsonWebTokenHandler
                    .Setup(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()))
                    .ReturnsAsync(new TokenValidationResult { IsValid = isTokenValid, });

                // Act
                var result = await Target.ValidateTokenAsync(token);

                // Assert
                Assert.Equal(isTokenValid, result.IsValid);

                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(
                    It.IsAny<JsonWebToken>(),
                    It.Is<TokenValidationParameters>(p =>
                        // MAKE SURE we validate the issuer and audience
                        p.ValidIssuer == GitHubTokenPolicyValidator.Issuer &&
                        p.ValidAudience == Configuration.Object.NuGetAudience &&

                        // MAKE SURE we validate the signing key and require signed tokens
                        p.ValidateIssuerSigningKey == true &&
                        p.RequireSignedTokens == true
                    )), Times.Once);
            }

            [Fact]
            public async Task WithMissingAudience_ThrowsException()
            {
                // Arrange
                Configuration.Setup(x => x.NuGetAudience).Returns((string)null!);

                var token = TokenTestHelper.CreateTestJwt();

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ValidateTokenAsync(token));
            }

            [Fact]
            public async Task VerifiesTokenValidationParameters()
            {
                // Arrange
                var token = TokenTestHelper.CreateTestJwt();

                JsonWebTokenHandler
                    .Setup(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()))
                    .ReturnsAsync(new TokenValidationResult { IsValid = true });

                // Act
                await Target.ValidateTokenAsync(token);

                // Assert
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(
                    It.IsAny<JsonWebToken>(),
                    It.Is<TokenValidationParameters>(p =>
                        p.ValidIssuer == GitHubTokenPolicyValidator.Issuer &&
                        p.ValidAudience == Configuration.Object.NuGetAudience &&
                        p.ConfigurationManager == OidcConfigManager.Object &&
                        p.ValidateIssuerSigningKey == true &&
                        p.RequireSignedTokens == true
                    )), Times.Once);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void RejectsMissingTokenIdentifierClaim(string? jtiValue)
            {
                // Arrange
                var token = TokenTestHelper.CreateTestJwtWithCustomClaimValue("jti", jtiValue);

                // Act
                var (tokenId, error) = Target.ValidateTokenIdentifier(token);

                // Assert
                Assert.Null(tokenId);
                Assert.Equal("The JSON web token must have a jti claim.", error);
            }
        }

        public class TheEvaluatePolicyMethod : GitHubTokenPolicyValidatorFacts
        {
            [Fact]
            public async Task ReturnsNotApplicableForNonGitHubActionsPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = "dummy"
                };

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.NotApplicable, result.Type);
            }

            [Fact]
            public async Task ThrowsInvalidCriteriaJson()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = "invalid"
                };

                var token = TokenTestHelper.CreateTestJwt();

                // Act and assert
                await Assert.ThrowsAnyAsync<Exception>(() => Target.EvaluatePolicyAsync(policy, token));
            }

            [Fact]
            public async Task RejectsExpiredTemporaryPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.ExpiredPolicyCriteria
                };

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.Equal("The policy has expired.", result.InternalReason);
            }

            [Theory]
            [InlineData("repository_owner")]
            [InlineData("repository_owner_id")]
            [InlineData("repository")]
            [InlineData("repository_id")]
            [InlineData("job_workflow_ref")]
            [InlineData("environment")]
            public async Task RejectsMissingRequiredClaim(string claim)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria
                };

                var tokenWithEmptyValue = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, null);
                var tokenWithMissingValue = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "");

                // Act
                var resultEmpty = await Target.EvaluatePolicyAsync(policy, tokenWithEmptyValue);
                var resultMissing = await Target.EvaluatePolicyAsync(policy, tokenWithMissingValue);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, resultEmpty.Type);
                Assert.Contains(claim, resultEmpty.InternalReason);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, resultMissing.Type);
                Assert.Contains(claim, resultMissing.InternalReason);
            }

            [Theory]
            [InlineData("repository_owner")]
            [InlineData("repository_owner_id")]
            [InlineData("repository")]
            [InlineData("repository_id")]
            [InlineData("job_workflow_ref")]
            [InlineData("environment")]
            public async Task RejectsMismatchedClaim(string claim)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria
                };

                var token = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "mismatched-value");

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);

                // Error string should contain the claim name, mismatched value and the expected value
                Assert.Contains(claim, result.InternalReason);
                Assert.Contains("mismatched-value", result.InternalReason);
            }

            [Theory]
            [InlineData("repository_owner_id", true)]
            [InlineData("repository_id", true)]
            [InlineData("repository_owner", false)]
            [InlineData("repository", false)]
            [InlineData("job_workflow_ref", false)]
            [InlineData("environment", false)]
            public async Task VerifyCaseSensitivity(string claim, bool isCaseSensitive)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria
                };

                string value = TokenTestHelper.ValidClaims[claim].ToString().ToUpperInvariant();
                var token = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, value);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                var expected = isCaseSensitive
                    ? FederatedCredentialPolicyResultType.Unauthorized
                    : FederatedCredentialPolicyResultType.Success;
                Assert.Equal(expected, result.Type);
            }

            [Theory]
            [InlineData(TokenTestHelper.PermanentPolicyCriteria)]
            [InlineData(TokenTestHelper.PermanentPolicyCriteriaNoEnvironment)]
            public async Task ReturnsSuccessForValidPermanentPolicy(string policyCriteria)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = policyCriteria
                };

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);
            }

            [Fact]
            public async Task ReturnsSuccessForValidTemporaryPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.TempotatyPolicyCriteria
                };

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                var dbCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria)!;
                Assert.True(dbCriteria.IsPermanentlyEnabled);
            }
        }

        public GitHubTokenPolicyValidatorFacts()
        {
            ConfigurationRetriever = new Mock<IConfigurationRetriever<OpenIdConnectConfiguration>>();
            OidcConfigManager = new Mock<ConfigurationManager<OpenIdConnectConfiguration>>(
                "https://token.actions.githubusercontent.com/.well-known/openid-configuration",
                ConfigurationRetriever.Object);
            JsonWebTokenHandler = new Mock<JsonWebTokenHandler>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            FederatedCredentialRepository = new Mock<IFederatedCredentialRepository>();

            // Setup test OIDC configuration with the same key used for token signing
            var oidcConfig = new OpenIdConnectConfiguration
            {
                JsonWebKeySet = new JsonWebKeySet
                {
                    Keys = { CreateTestJsonWebKey() }
                }
            };

            OidcConfigManager.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(oidcConfig);

            // Setup configuration
            Configuration.Setup(x => x.NuGetAudience).Returns(TokenTestHelper.ValidAudience);

            Target = new GitHubTokenPolicyValidator(
                FederatedCredentialRepository.Object,
                OidcConfigManager.Object,
                JsonWebTokenHandler.Object,
                Configuration.Object);
        }

        public GitHubTokenPolicyValidator Target { get; }
        public Mock<IConfigurationRetriever<OpenIdConnectConfiguration>> ConfigurationRetriever { get; }
        public Mock<ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigManager { get; }
        public Mock<JsonWebTokenHandler> JsonWebTokenHandler { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public Mock<IFederatedCredentialRepository> FederatedCredentialRepository { get; }

        private JsonWebKey CreateTestJsonWebKey()
        {
            var jsonWebKey = JsonWebKeyConverter.ConvertFromSymmetricSecurityKey(TokenTestHelper.DefaultSigningKey);
            jsonWebKey.Kid = "test-key-id";
            return jsonWebKey;
        }
    }
}
