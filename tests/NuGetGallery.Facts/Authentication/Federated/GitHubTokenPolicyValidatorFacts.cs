// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
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

        public const string TemporaryPolicyCriteria = """
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

    public class TheValidatePolicyMethod : GitHubTokenPolicyValidatorFacts
    {
        [Fact]
        public void RejectsNonGitHubActionsPolicy()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.EntraIdServicePrincipal,
                CreatedBy = user,
                Criteria = "dummy"
            };

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
            Assert.Equal("Invalid policy type 'EntraIdServicePrincipal' for GitHub Actions validation", result.UserMessage);
            Assert.Null(result.PolicyPropertyName);
        }

        [Fact]
        public void RejectsWhenTrustedPublishingDisabled()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                Criteria = TokenTestHelper.PermanentPolicyCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(false);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
            Assert.Equal("Trusted Publishing is not enabled for 'test-user'.", result.UserMessage);
            Assert.Equal(nameof(FederatedCredentialPolicy.CreatedBy), result.PolicyPropertyName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EnsuresNonEmptyPolicyName(string? policyName)
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = policyName,
                Criteria = TokenTestHelper.PermanentPolicyCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
            Assert.Equal("test", policy.PolicyName);
        }

        [Fact]
        public void RejectsInvalidCriteriaJson()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = "invalid json"
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => Target.ValidatePolicy(policy));
        }

        [Theory]
        [InlineData("""{"owner":"test-owner","repository":"test-repo"}""")] // Missing workflow
        [InlineData("""{"owner":"test-owner","workflow":"test.yml"}""")] // Missing repository
        [InlineData("""{"repository":"test-repo","workflow":"test.yml"}""")] // Missing owner
        [InlineData("""{"owner":"","repository":"test-repo","workflow":"test.yml"}""")] // Empty owner
        [InlineData("""{"owner":"test-owner","repository":"","workflow":"test.yml"}""")] // Empty repository
        [InlineData("""{"owner":"test-owner","repository":"test-repo","workflow":""}""")] // Empty workflow
        public void RejectsInvalidCriteria(string invalidCriteria)
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = invalidCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
            Assert.NotNull(result.UserMessage);
            Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
        }

        [Fact]
        public void AcceptsValidPermanentPolicy()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = TokenTestHelper.PermanentPolicyCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
            Assert.Null(result.UserMessage);
            Assert.Null(result.PolicyPropertyName);
            Assert.NotNull(result.Policy);
        }

        [Fact]
        public void AcceptsValidTemporaryPolicy()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml"}"""
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
            Assert.Null(result.UserMessage);
            Assert.Null(result.PolicyPropertyName);
            Assert.NotNull(result.Policy);

            // Verify that the policy criteria was updated with validateBy date
            var updatedCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);
            Assert.NotNull(updatedCriteria.ValidateByDate);
            Assert.True(updatedCriteria.ValidateByDate > DateTimeOffset.UtcNow);
        }

        [Fact]
        public void InitializesValidateByDateForTemporaryPolicy()
        {
            // Arrange
            var user = new User("test-user");
            var originalCriteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml"}""";
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = originalCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);

            // Verify that the criteria was modified to include validateBy
            Assert.NotEqual(originalCriteria, policy.Criteria);
            var updatedCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);
            Assert.NotNull(updatedCriteria.ValidateByDate);
            Assert.True(updatedCriteria.ValidateByDate > DateTimeOffset.UtcNow);
        }

        [Fact]
        public void DoesNotModifyValidateByDateForPermanentPolicy()
        {
            // Arrange
            var user = new User("test-user");
            var originalCriteria = TokenTestHelper.PermanentPolicyCriteria;
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = originalCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);

            // Verify that permanent policy criteria remains unchanged (no validateBy date added)
            var updatedCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);
            Assert.Null(updatedCriteria.ValidateByDate);
            Assert.True(updatedCriteria.IsPermanentlyEnabled);
        }

        [Fact]
        public void ResetsValidateByDateForTemporaryPolicyOnValidation()
        {
            // Arrange
            var user = new User("test-user");
            var pastDate = DateTimeOffset.UtcNow.AddDays(-1);
            var expiredCriteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml","validateBy":"2025-01-01T01:00:00+00:00"}""";
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = expiredCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);

            // Verify that the validateBy date was reset to a future date
            var updatedCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria);
            Assert.NotNull(updatedCriteria.ValidateByDate);
            Assert.True(updatedCriteria.ValidateByDate > DateTimeOffset.UtcNow);
        }

        [Fact]
        public void AcceptsPolicyWithEnvironment()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml","environment":"production"}"""
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
        }

        [Fact]
        public void CallsBaseValidatePolicyForValidInput()
        {
            // Arrange
            var user = new User("test-user");
            var policy = new FederatedCredentialPolicy
            {
                Type = FederatedCredentialType.GitHubActions,
                CreatedBy = user,
                PolicyName = "Test Policy",
                Criteria = TokenTestHelper.PermanentPolicyCriteria
            };

            FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(user)).Returns(true);

            // Act
            var result = Target.ValidatePolicy(policy);

            // Assert
            Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
        }
    }

    public class GitHubTokenPolicyValidatorFacts
    {
        public class TheValidateTokenAsyncMethod : GitHubTokenPolicyValidatorFacts
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task ValidateTokenAsync_CallsJsonWebTokenHandlerWithCorrectParameters(bool isTokenValid)
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
                        p.ValidAudience == TokenTestHelper.ValidAudience &&

                        // MAKE SURE we validate the signing key and require signed tokens
                        p.ValidateLifetime == true &&
                        p.RequireExpirationTime == true &&
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
            public async Task RejectsWhenTrustedPublishingDisabled()
            {
                // Arrange
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(false);

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.Equal("Trusted publishing is not enabled for test-user", result.Error);
            }

            [Fact]
            public async Task ThrowsInvalidCriteriaJson()
            {
                // Arrange
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = "invalid",
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwt();

                // Act and assert
                await Assert.ThrowsAnyAsync<Exception>(() => Target.EvaluatePolicyAsync(policy, token));
            }

            [Fact]
            public async Task RejectsExpiredTemporaryPolicy()
            {
                // Arrange
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    PolicyName = "Temporary Policy",
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.ExpiredPolicyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.True(result.IsErrorDisclosable); // we disclose the error for expired policies
                Assert.Contains("expired", result.Error);
                Assert.Contains(policy.PolicyName, result.Error);
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
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var tokenWithEmptyValue = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, null);
                var tokenWithMissingValue = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "");

                // Act
                var resultEmpty = await Target.EvaluatePolicyAsync(policy, tokenWithEmptyValue);
                var resultMissing = await Target.EvaluatePolicyAsync(policy, tokenWithMissingValue);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, resultEmpty.Type);
                Assert.False(resultEmpty.IsErrorDisclosable);
                Assert.Contains(claim, resultEmpty.Error);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, resultMissing.Type);
                Assert.False(resultMissing.IsErrorDisclosable);
                Assert.Contains(claim, resultMissing.Error);
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
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "mismatched-value");

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);

                // Error string should contain the claim name and the mismatched value.
                Assert.Contains(claim, result.Error);
                Assert.Contains("mismatched-value", result.Error);
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
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

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
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = policyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

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
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = createdBy,
                    PackageOwner = new User("test-owner")
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                var dbCriteria = GitHubCriteria.FromDatabaseJson(policy.Criteria)!;
                Assert.True(dbCriteria.IsPermanentlyEnabled);

                // Verify that the first-time use audit was saved
                AuditingService.Verify(x => x.SaveAuditRecordAsync(It.Is<FederatedCredentialPolicyAuditRecord>(
                    audit => audit.Action == AuditedFederatedCredentialPolicyAction.FirstUsePolicyUpdate)), Times.Once);
            }

            [Fact]
            public async Task HandlesConcurrentFirstUseScenario()
            {
                // Arrange
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Key = 123,
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = createdBy
                };
                var updatedPolicy = new FederatedCredentialPolicy
                {
                    Key = policy.Key,
                    Type = policy.Type,
                    Criteria = TokenTestHelper.PermanentPolicyCriteria
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwt();

                // Setup SavePoliciesAsync to throw DbUpdateConcurrencyException on first call
                FederatedCredentialRepository
                    .SetupSequence(x => x.SavePoliciesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException())
                    .Returns(Task.CompletedTask);

                FederatedCredentialRepository
                    .Setup(x => x.GetPolicyByKey(123))
                    .Returns(updatedPolicy);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                FederatedCredentialRepository.Verify(x => x.GetPolicyByKey(123), Times.Once);
                AuditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
            }

            [Theory]
            [InlineData("wrong-owner-id", null)]
            [InlineData(null, "wrong-repo-id")]
            [InlineData("wrong-owner-id", "wrong-repo-id")]
            public async Task RejectsConcurrentFirstUseWithDifferentIds(string? ownerId, string? repoId)
            {
                // Arrange
                var createdBy = new User("test-user");
                GitHubCriteria updatedCriteria = GitHubCriteria.FromDatabaseJson(TokenTestHelper.PermanentPolicyCriteria)!;
                updatedCriteria.RepositoryOwnerId = ownerId ?? updatedCriteria.RepositoryOwnerId;
                updatedCriteria.RepositoryId = repoId ?? updatedCriteria.RepositoryId;

                var policy = new FederatedCredentialPolicy
                {
                    Key = 123,
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = createdBy
                };

                var updatedPolicy = new FederatedCredentialPolicy
                {
                    Key = policy.Key,
                    Type = policy.Type,
                    Criteria = updatedCriteria.ToDatabaseJson()
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwt();

                // Setup SavePoliciesAsync to throw DbUpdateConcurrencyException
                FederatedCredentialRepository
                    .Setup(x => x.SavePoliciesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

                FederatedCredentialRepository
                    .Setup(x => x.GetPolicyByKey(123))
                    .Returns(updatedPolicy);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                FederatedCredentialRepository.Verify(x => x.GetPolicyByKey(123), Times.Once);
            }

            [Fact]
            public async Task HandlesConcurrentFirstUseWhenPolicyDeleted()
            {
                // Arrange
                var createdBy = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Key = 123,
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = TokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = createdBy
                };

                FeatureFlagService.Setup(x => x.IsTrustedPublishingEnabled(createdBy)).Returns(true);

                var token = TokenTestHelper.CreateTestJwt();

                // Setup SavePoliciesAsync to throw DbUpdateConcurrencyException
                FederatedCredentialRepository
                    .Setup(x => x.SavePoliciesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

                // Setup GetPolicyByKey to return null (policy was deleted)
                FederatedCredentialRepository
                    .Setup(x => x.GetPolicyByKey(123))
                    .Returns((FederatedCredentialPolicy?)null);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                FederatedCredentialRepository.Verify(x => x.GetPolicyByKey(123), Times.Once);
            }
        }

        public GitHubTokenPolicyValidatorFacts()
        {
            ConfigurationRetriever = new Mock<IConfigurationRetriever<OpenIdConnectConfiguration>>();
            OidcConfigManager = new Mock<ConfigurationManager<OpenIdConnectConfiguration>>(
                "https://token.actions.githubusercontent.com/.well-known/openid-configuration",
                ConfigurationRetriever.Object);

            FederatedCredentialRepository = new Mock<IFederatedCredentialRepository>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            AuditingService = new Mock<IAuditingService>();
            FeatureFlagService = new Mock<IFeatureFlagService>();
            JsonWebTokenHandler = new Mock<JsonWebTokenHandler>();

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

            Configuration.Setup(x => x.NuGetAudience).Returns(TokenTestHelper.ValidAudience);

            Target = new GitHubTokenPolicyValidator(
                FederatedCredentialRepository.Object,
                OidcConfigManager.Object,
                Configuration.Object,
                AuditingService.Object,
                FeatureFlagService.Object,
                JsonWebTokenHandler.Object);
        }

        public GitHubTokenPolicyValidator Target { get; }
        public Mock<IConfigurationRetriever<OpenIdConnectConfiguration>> ConfigurationRetriever { get; }
        public Mock<ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigManager { get; }
        public Mock<JsonWebTokenHandler> JsonWebTokenHandler { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public Mock<IFederatedCredentialRepository> FederatedCredentialRepository { get; }
        public Mock<IAuditingService> AuditingService { get; }
        public Mock<IFeatureFlagService> FeatureFlagService { get; }


        private JsonWebKey CreateTestJsonWebKey()
        {
            var jsonWebKey = JsonWebKeyConverter.ConvertFromSymmetricSecurityKey(TokenTestHelper.DefaultSigningKey);
            jsonWebKey.Kid = "test-key-id";
            return jsonWebKey;
        }
    }
}
