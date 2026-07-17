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
    public class GitLabTokenTestHelper
    {
        public static readonly Dictionary<string, object> ValidClaims = new()
        {
            { "namespace_path", "test-group" },
            { "namespace_id",   "id-111" },
            { "project_path",   "test-group/test-project" },
            { "project_id",     "id-222" },
            { "ref",            "main" },
            { "environment",    "production" },
        };

        public const string PermanentPolicyCriteria = """
            {
                "namespacePath": "test-group",
                "namespaceId": "id-111",
                "projectPath": "test-project",
                "projectId": "id-222",
                "environment": "production"
            }
            """;

        public const string PermanentPolicyCriteriaNoEnvironment = """
            {
                "namespacePath": "test-group",
                "namespaceId": "id-111",
                "projectPath": "test-project",
                "projectId": "id-222"
            }
            """;

        public const string TemporaryPolicyCriteria = """
            {
                "namespacePath": "test-group",
                "projectPath": "test-project",
                "environment": "production",
                "validateBy": "2222-01-01T00:00:00Z"
            }
            """;

        public static readonly string ExpiredPolicyCriteria = """
            {
                "namespacePath": "test-group",
                "projectPath": "test-project",
                "environment": "production",
                "validateBy": "1999-01-01T00:00:00Z"
            }
            """;

        public static readonly string ValidIssuer = GitLabTokenPolicyValidator.Issuer;
        public static readonly string ValidAudience = "nuget.org";

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
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
            };
            var tokenString = handler.CreateToken(tokenDescriptor);
            return handler.ReadJsonWebToken(tokenString);
        }

        public static SymmetricSecurityKey CreateTestSymmetricKey(string keyMaterial = "your-256-bit-secret-key-here-32-chars")
        {
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyMaterial));
        }
    }

    public class GitLabTokenPolicyValidatorFacts
    {
        public class TheEvaluatePolicyMethod : GitLabTokenPolicyValidatorFacts
        {
            [Fact]
            public async Task ReturnsNotApplicableForNonGitLabCIPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = "dummy"
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.NotApplicable, result.Type);
            }

            // ----- Required claims -----

            [Theory]
            [InlineData("namespace_path")]
            [InlineData("project_path")]
            [InlineData("namespace_id")]
            [InlineData("project_id")]
            public async Task RejectsMissingRequiredClaim(string claim)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var tokenWithMissingClaim = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, null);
                var tokenWithEmptyClaim = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "");

                // Act
                var resultMissing = await Target.EvaluatePolicyAsync(policy, tokenWithMissingClaim);
                var resultEmpty = await Target.EvaluatePolicyAsync(policy, tokenWithEmptyClaim);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, resultMissing.Type);
                Assert.True(resultMissing.IsErrorDisclosable);
                Assert.Contains(claim, resultMissing.Error);

                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, resultEmpty.Type);
                Assert.True(resultEmpty.IsErrorDisclosable);
                Assert.Contains(claim, resultEmpty.Error);
            }

            // ----- Path matching -----

            [Theory]
            [InlineData("namespace_path", false)]
            [InlineData("project_path", false)]
            public async Task RejectsMismatchedPathClaim(string claim, bool isErrorDisclosable)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    PolicyName = "TestPolicy",
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "mismatched-value");

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.Equal(isErrorDisclosable, result.IsErrorDisclosable);
            }

            // ----- TOFU: expired policy -----

            [Fact]
            public async Task RejectsExpiredTemporaryPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    PolicyName = "Temporary Policy",
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.ExpiredPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.True(result.IsErrorDisclosable);
                Assert.Contains("expired", result.Error);
                Assert.Contains(policy.PolicyName, result.Error);
            }

            // ----- TOFU: first use -----

            [Fact]
            public async Task FirstUse_LocksInNamespaceAndProjectIds()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = new User("test-user"),
                    PackageOwner = new User("test-owner")
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);

                // IDs from the token must be locked in
                var lockedCriteria = GitLabCriteria.FromDatabaseJson(policy.Criteria);
                Assert.True(lockedCriteria.IsPermanentlyEnabled);
                Assert.Equal("id-111", lockedCriteria.NamespaceId);
                Assert.Equal("id-222", lockedCriteria.ProjectId);
                Assert.Null(lockedCriteria.ValidateByDate);

                // Must persist and audit the first use
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                AuditingService.Verify(x => x.SaveAuditRecordAsync(It.Is<FederatedCredentialPolicyAuditRecord>(
                    audit => audit.Action == AuditedFederatedCredentialPolicyAction.FirstUsePolicyUpdate)), Times.Once);
            }

            // ----- TOFU: subsequent uses -----

            [Theory]
            [InlineData(GitLabTokenTestHelper.PermanentPolicyCriteria)]
            [InlineData(GitLabTokenTestHelper.PermanentPolicyCriteriaNoEnvironment)]
            public async Task SubsequentUse_SucceedsWithMatchingIds(string criteria)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = criteria,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                // Must NOT touch the DB on subsequent uses
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);
            }

            [Theory]
            [InlineData("namespace_id", true)]
            [InlineData("project_id", true)]
            public async Task SubsequentUse_VerifyIdCaseSensitivity(string claim, bool isCaseSensitive)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                string upperValue = GitLabTokenTestHelper.ValidClaims[claim].ToString()!.ToUpperInvariant();
                var token = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, upperValue);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                var expected = isCaseSensitive
                    ? FederatedCredentialPolicyResultType.Unauthorized
                    : FederatedCredentialPolicyResultType.Success;
                Assert.Equal(expected, result.Type);
            }

            [Theory]
            [InlineData("namespace_id")]
            [InlineData("project_id")]
            public async Task SubsequentUse_RejectsMismatchedId(string claim)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue(claim, "wrong-id");

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
            }

            // ----- Optional claims: ref and environment -----

            [Fact]
            public async Task RejectsMismatchedRef()
            {
                // Arrange
                var criteriaWithRef = new GitLabCriteria
                {
                    NamespacePath = "test-group",
                    NamespaceId = "id-111",
                    ProjectPath = "test-project",
                    ProjectId = "id-222",
                    Ref = "main"
                }.ToDatabaseJson();

                var policy = new FederatedCredentialPolicy
                {
                    PolicyName = "TestPolicy",
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = criteriaWithRef,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue("ref", "feature-branch");

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.True(result.IsErrorDisclosable);
                Assert.Contains("main", result.Error);
                Assert.Contains("feature-branch", result.Error);
            }

            [Fact]
            public async Task RejectsMismatchedEnvironment()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    PolicyName = "TestPolicy",
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.PermanentPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwtWithCustomClaimValue("environment", "staging");

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.True(result.IsErrorDisclosable);
                Assert.Contains("production", result.Error);
                Assert.Contains("staging", result.Error);
            }

            // ----- Concurrent first use -----

            [Fact]
            public async Task HandlesConcurrentFirstUseWithSameIds()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Key = 123,
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var concurrentlyLockedCriteria = new GitLabCriteria
                {
                    NamespacePath = "test-group",
                    NamespaceId = "id-111",
                    ProjectPath = "test-project",
                    ProjectId = "id-222",
                }.ToDatabaseJson();

                var updatedPolicy = new FederatedCredentialPolicy
                {
                    Key = policy.Key,
                    Type = policy.Type,
                    Criteria = concurrentlyLockedCriteria
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

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
                // No audit for the losing writer
                AuditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
            }

            [Theory]
            [InlineData("wrong-namespace-id", null)]
            [InlineData(null, "wrong-project-id")]
            [InlineData("wrong-namespace-id", "wrong-project-id")]
            public async Task RejectsConcurrentFirstUseWithDifferentIds(string? namespaceId, string? projectId)
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Key = 123,
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var updatedCriteria = GitLabCriteria.FromDatabaseJson(GitLabTokenTestHelper.PermanentPolicyCriteria);
                updatedCriteria.NamespaceId = namespaceId ?? updatedCriteria.NamespaceId;
                updatedCriteria.ProjectId = projectId ?? updatedCriteria.ProjectId;

                var updatedPolicy = new FederatedCredentialPolicy
                {
                    Key = policy.Key,
                    Type = policy.Type,
                    Criteria = updatedCriteria.ToDatabaseJson()
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

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
                var policy = new FederatedCredentialPolicy
                {
                    Key = 123,
                    Type = FederatedCredentialType.GitLabCI,
                    Criteria = GitLabTokenTestHelper.TemporaryPolicyCriteria,
                    CreatedBy = new User("test-user")
                };

                var token = GitLabTokenTestHelper.CreateTestJwt();

                FederatedCredentialRepository
                    .Setup(x => x.SavePoliciesAsync())
                    .ThrowsAsync(new DbUpdateConcurrencyException());

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

        public GitLabTokenPolicyValidatorFacts()
        {
            ConfigurationRetriever = new Mock<IConfigurationRetriever<OpenIdConnectConfiguration>>();
            OidcConfigManager = new Mock<ConfigurationManager<OpenIdConnectConfiguration>>(
                GitLabTokenPolicyValidator.MetadataAddress,
                ConfigurationRetriever.Object);

            FederatedCredentialRepository = new Mock<IFederatedCredentialRepository>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            AuditingService = new Mock<IAuditingService>();
            FeatureFlagService = new Mock<IFeatureFlagService>();
            JsonWebTokenHandler = new Mock<JsonWebTokenHandler>();

            var oidcConfig = new OpenIdConnectConfiguration
            {
                JsonWebKeySet = new JsonWebKeySet
                {
                    Keys = { CreateTestJsonWebKey() }
                }
            };

            OidcConfigManager
                .Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(oidcConfig);

            Configuration.Setup(x => x.NuGetAudience).Returns(GitLabTokenTestHelper.ValidAudience);

            Target = new GitLabTokenPolicyValidator(
                FederatedCredentialRepository.Object,
                OidcConfigManager.Object,
                Configuration.Object,
                AuditingService.Object,
                FeatureFlagService.Object,
                JsonWebTokenHandler.Object);
        }

        public GitLabTokenPolicyValidator Target { get; }
        public Mock<IConfigurationRetriever<OpenIdConnectConfiguration>> ConfigurationRetriever { get; }
        public Mock<ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigManager { get; }
        public Mock<JsonWebTokenHandler> JsonWebTokenHandler { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public Mock<IFederatedCredentialRepository> FederatedCredentialRepository { get; }
        public Mock<IAuditingService> AuditingService { get; }
        public Mock<IFeatureFlagService> FeatureFlagService { get; }

        private JsonWebKey CreateTestJsonWebKey()
        {
            var jsonWebKey = JsonWebKeyConverter.ConvertFromSymmetricSecurityKey(GitLabTokenTestHelper.DefaultSigningKey);
            jsonWebKey.Kid = "test-key-id";
            return jsonWebKey;
        }
    }
}
