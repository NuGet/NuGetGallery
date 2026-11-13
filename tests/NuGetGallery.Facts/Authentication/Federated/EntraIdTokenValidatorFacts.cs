// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NuGet.Services.Entities;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class EntraIdTokenValidatorFacts
    {
        public class TheValidatePolicyMethod : EntraIdTokenValidatorFacts
        {
            [Fact]
            public void RejectsNonEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    PackageOwner = user,
                    Criteria = "dummy"
                };

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("Invalid policy type 'GitHubActions' for Entra ID validation.", result.UserMessage);
                Assert.Null(result.PolicyPropertyName);
            }

            [Theory]
            [InlineData(null, FederatedCredentialType.EntraIdServicePrincipal)]
            [InlineData("", FederatedCredentialType.EntraIdServicePrincipal)]
            [InlineData("   ", FederatedCredentialType.EntraIdServicePrincipal)]
            [InlineData(null, FederatedCredentialType.AzureDevOps)]
            [InlineData("", FederatedCredentialType.AzureDevOps)]
            [InlineData("   ", FederatedCredentialType.AzureDevOps)]
            public void RejectsEmptyOrWhitespaceCriteria(string? criteria, FederatedCredentialType type)
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = type,
                    PackageOwner = user,
                    Criteria = criteria
                };

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal($"Criteria must be provided for {type} policies.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
            }

            [Theory]
            [InlineData(FederatedCredentialType.EntraIdServicePrincipal)]
            [InlineData(FederatedCredentialType.AzureDevOps)]
            public void RejectsInvalidJsonCriteria(FederatedCredentialType type)
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = type,
                    PackageOwner = user,
                    Criteria = "invalid json"
                };

                // Act & Assert
                Assert.ThrowsAny<Exception>(() => Target.ValidatePolicy(policy));
            }

            [Theory]
            [InlineData(FederatedCredentialType.EntraIdServicePrincipal)]
            [InlineData(FederatedCredentialType.AzureDevOps)]
            public void RejectsNullCriteriaObject(FederatedCredentialType type)
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = type,
                    PackageOwner = user,
                    Criteria = "null"
                };

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal($"Invalid criteria format for {type} policy.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
            }

            [Theory]
            [InlineData(FederatedCredentialType.EntraIdServicePrincipal, """{"tid":"c311b905-19a2-483e-a014-41d0fcdc99cf","oid":"d17083b8-74e0-46c6-b69f-764da2e6fc0e"}""")]
            [InlineData(FederatedCredentialType.AzureDevOps, """{"organizationId":"0c25520f-c7eb-4bc1-919c-3870a18620ef","pipelineId":"12345"}""")]
            public void CallsBaseValidatePolicyForValidInput(FederatedCredentialType type, string criteria)
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = type,
                    PackageOwner = user,
                    Criteria = criteria,
                };

                AllowedTenantIds = [TenantId.ToString()];

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                // The fact that we get Success indicates base.ValidatePolicy() was called and succeeded
            }

            [Fact]
            public void RejectsWhenFederatedCredentialsDisabled()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(user)).Returns(false);

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("The package owner 'test-user' is not enabled to use federated credentials.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.PackageOwner), result.PolicyPropertyName);
            }

            [Fact]
            public void RejectsNotAllowedTenantForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var notAllowedTenantId = new Guid("d8f0bfc3-5def-4079-b08c-618832b6ae16");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(notAllowedTenantId, ObjectId))
                };

                AllowedTenantIds = [TenantId.ToString()]; // Only allow the default tenant

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Unauthorized, result.Type);
                Assert.Equal($"The Entra ID tenant '{notAllowedTenantId}' is not in the allow list.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
            }

            [Fact]
            public void RejectsWhenNoTenantsAllowedForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                AllowedTenantIds = []; // No tenants allowed

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Unauthorized, result.Type);
                Assert.Equal($"The Entra ID tenant '{TenantId}' is not in the allow list.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
            }

            [Fact]
            public void AcceptsValidPolicyWithAllowedTenantForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                AllowedTenantIds = [TenantId.ToString()];

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Null(result.UserMessage);
                Assert.Null(result.PolicyPropertyName);
                Assert.NotNull(result.Policy);
            }

            [Fact]
            public void AcceptsValidPolicyWhenAllTenantsAllowedForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                AllowedTenantIds = ["all"]; // All tenants allowed

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Null(result.UserMessage);
                Assert.Null(result.PolicyPropertyName);
                Assert.NotNull(result.Policy);
            }

            [Fact]
            public void AcceptsValidPolicyWithCaseInsensitiveTenantMatchingForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                AllowedTenantIds = [TenantId.ToString().ToUpperInvariant()]; // Uppercase tenant ID

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Null(result.UserMessage);
                Assert.Null(result.PolicyPropertyName);
                Assert.NotNull(result.Policy);
            }

            [Fact]
            public void AcceptsValidPolicyWithMultipleAllowedTenantsForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                AllowedTenantIds = [
                    "d8f0bfc3-5def-4079-b08c-618832b6ae16",
                    TenantId.ToString(),
                    "a1b2c3d4-e5f6-7890-1234-567890abcdef"
                ];

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Null(result.UserMessage);
                Assert.Null(result.PolicyPropertyName);
                Assert.NotNull(result.Policy);
            }

            [Fact]
            public void RejectsWhenAzureDevOpsCredentialsDisabled()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.AzureDevOps,
                    PackageOwner = user,
                    Criteria = """{"organizationId":"7346afa9-c5f0-474b-a335-b2f802b69012","pipelineId":"12345"}"""
                };

                FeatureFlagService.Setup(x => x.CanUseAzureDevOpsCredentials(user)).Returns(false);
                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("The package owner 'test-user' is not enabled to use Azure DevOps credentials.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.PackageOwner), result.PolicyPropertyName);
            }

            [Theory]
            [InlineData("""{"organizationId":"   ","pipelineId":" "}""",
                        "Invalid criteria format for AzureDevOps policy: The organization ID and pipeline ID are empty.")]
            [InlineData("""{"organizationId":"","pipelineId":"12345"}""",
                        "Invalid criteria format for AzureDevOps policy: The organization ID is empty.")]
            [InlineData("""{"organizationId":"0c25520f-c7eb-4bc1-919c-3870a18620ef","pipelineId":""}""",
                        "Invalid criteria format for AzureDevOps policy: The pipeline ID is empty.")]
            public void RejectsInvalidCriteriaForAzureDevOpsPolicy(string criteria, string expectedUserMessage)
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.AzureDevOps,
                    PackageOwner = user,
                    Criteria = criteria,
                };

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal(expectedUserMessage, result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
            }
        }

        public class TheValidateAsyncMethod : EntraIdTokenValidatorFacts
        {
            [Fact]
            public async Task RejectsMissingAudience()
            {
                // Arrange
                Configuration.Setup(x => x.EntraIdAudience).Returns((string?)null);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ValidateTokenAsync(Token));
            }

            [Fact]
            public async Task ReturnsTokenValidationResultDirectly()
            {
                // Arrange
                var result = new TokenValidationResult();
                JsonWebTokenHandler
                    .Setup(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()))
                    .ReturnsAsync(result);

                // Act
                var actual = await Target.ValidateTokenAsync(Token);

                // Assert
                Assert.Same(result, actual);
            }

            [Fact]
            public async Task ConfiguresTokenValidationParameters()
            {
                // Arrange
                var token = Token;

                // Act
                await Target.ValidateTokenAsync(token);

                // Assert
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(token, It.IsAny<TokenValidationParameters>()), Times.Once);
                var invocation = Assert.Single(JsonWebTokenHandler.Invocations);
                var tokenValidationParameters = (TokenValidationParameters)invocation.Arguments[1];
                Assert.Equal("nuget-audience", tokenValidationParameters.ValidAudience);
                Assert.NotNull(tokenValidationParameters.IssuerValidator);
                Assert.NotNull(tokenValidationParameters.IssuerSigningKeyValidatorUsingConfiguration);
                Assert.Same(OidcConfigManager.Object, tokenValidationParameters.ConfigurationManager);
            }

            [Fact]
            public async Task AcceptsEntraIdIssuer()
            {
                // Arrange
                await Target.ValidateTokenAsync(Token);
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()), Times.Once);
                var invocation = Assert.Single(JsonWebTokenHandler.Invocations);
                var tokenValidationParameters = (TokenValidationParameters)invocation.Arguments[1];

                // Act
                var issuer = tokenValidationParameters.IssuerValidator(Issuer, Token, tokenValidationParameters);

                // Assert
                Assert.Equal(Issuer, issuer);
            }

            [Fact]
            public async Task RejectsOtherIssuer()
            {
                // Arrange
                Issuer = "https://localhost/my-issuer";
                await Target.ValidateTokenAsync(Token);
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()), Times.Once);
                var invocation = Assert.Single(JsonWebTokenHandler.Invocations);
                var tokenValidationParameters = (TokenValidationParameters)invocation.Arguments[1];

                // Act & Assert
                Assert.Throws<SecurityTokenInvalidIssuerException>(
                    () => tokenValidationParameters.IssuerValidator(Issuer, Token, tokenValidationParameters));
            }

            [Fact]
            public async Task AcceptsSigningKeyWithMatchingIssuer()
            {
                // Arrange
                await Target.ValidateTokenAsync(Token);
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()), Times.Once);
                var invocation = Assert.Single(JsonWebTokenHandler.Invocations);
                var tokenValidationParameters = (TokenValidationParameters)invocation.Arguments[1];

                // Act
                var valid = tokenValidationParameters.IssuerSigningKeyValidatorUsingConfiguration(JsonWebKey, Token, tokenValidationParameters, OpenIdConnectConfiguration);

                // Assert
                Assert.True(valid);
            }

            [Fact]
            public async Task RejectsSigningKeyWithDifferentIssuer()
            {
                // Arrange
                await Target.ValidateTokenAsync(Token);
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()), Times.Once);
                var invocation = Assert.Single(JsonWebTokenHandler.Invocations);
                var tokenValidationParameters = (TokenValidationParameters)invocation.Arguments[1];
                var key = new JsonWebKey { Kid = "key-id", AdditionalData = { { "issuer", "https://localhost/my-issuer" } } };
                var config = new OpenIdConnectConfiguration { JsonWebKeySet = new JsonWebKeySet { Keys = { key } } };

                // Act & Assert
                Assert.Throws<SecurityTokenInvalidIssuerException>(
                    () => tokenValidationParameters.IssuerSigningKeyValidatorUsingConfiguration(key, Token, tokenValidationParameters, config));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void RejectsMissingTokenIdentifierClaim(string? utiValue)
            {
                // Arrange
                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId },
                    { "iss", Issuer },
                };

                if (utiValue != null)
                {
                    claims.Add("uti", utiValue);
                }

                var token = CreateToken(claims);

                // Act
                var (tokenId, error) = Target.ValidateTokenIdentifier(token);

                // Assert
                Assert.Null(tokenId);
                Assert.Equal("The JSON web token must have a uti claim.", error);
            }
        }

        public class TheEvaluatePolicyMethod : EntraIdTokenValidatorFacts
        {
            [Fact]
            public async Task ReturnsNotApplicableForNonEntraIdPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    Criteria = JsonSerializer.Serialize(new { test = "value" })
                };

                var token = Token;

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.NotApplicable, result.Type);
            }

            [Theory]
            [InlineData("tid")]
            [InlineData("oid")]
            [InlineData("azpacr")]
            [InlineData("idtyp")]
            [InlineData("ver")]
            public async Task RejectsMissingClaim(string claim)
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };
                claims.Remove(claim);

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal($"The JSON Web Token is missing the required claim '{claim}'.", result.Error);
            }

            [Fact]
            public async Task RejectsInvalidCredentialType()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "1" }, // Invalid credential type
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The JSON web token must have an azpacr claim with a value of 2.", result.Error);
            }

            [Fact]
            public async Task RejectsInvalidIdentityType()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app+user" }, // Invalid identity type
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The JSON web token must have an idtyp claim with a value of app.", result.Error);
            }

            [Fact]
            public async Task RejectsInvalidVersion()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "1.0" }, // Invalid version
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The JSON web token must have a ver claim with a value of 2.0.", result.Error);
            }

            [Fact]
            public async Task RejectsOidNotMatchingSub()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", "different-client-id" }, // Different from oid
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The JSON web token sub claim must match the oid claim.", result.Error);
            }

            [Fact]
            public async Task RejectsWrongTenantId()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", "d8f0bfc3-5def-4079-b08c-618832b6ae16" }, // Different tenant ID
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The JSON web token must have a tid claim that matches the policy.", result.Error);
            }

            [Fact]
            public async Task RejectsNotAllowedTenantId()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                AllowedTenantIds = ["different-tenant-id"];

                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The tenant ID in the JSON web token is not in allow list.", result.Error);
            }

            [Fact]
            public async Task RejectsWrongObjectId()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var differentObjectId = new Guid("d8f0bfc3-5def-4079-b08c-618832b6ae16");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", differentObjectId.ToString() }, // Different object ID
                    { "sub", differentObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.False(result.IsErrorDisclosable);
                Assert.Equal("The JSON web token must have a oid claim that matches the policy.", result.Error);
            }

            [Fact]
            public async Task ReturnsSuccessForValidPolicy()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);

                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
            }

            [Fact]
            public async Task RejectsWhenFeatureFlagDisabled()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(false);

                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act
                var result = await Target.EvaluatePolicyAsync(policy, token);

                // Assert
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.Equal("The package owner 'test-user' is not enabled to use federated credentials.", result.Error);
            }

            [Fact]
            public async Task RejectsInvalidCriteriaJson()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = "invalid",
                    PackageOwner = PackageOwner
                };

                var claims = new Dictionary<string, object>
                {
                    { "tid", TenantId.ToString() },
                    { "oid", ObjectId.ToString() },
                    { "sub", ObjectId.ToString() },
                    { "azpacr", "2" },
                    { "idtyp", "app" },
                    { "ver", "2.0" },
                };

                var token = CreateToken(claims);

                // Act & Assert
                await Assert.ThrowsAnyAsync<Exception>(() => Target.EvaluatePolicyAsync(policy, token));
            }
        }

        public EntraIdTokenValidatorFacts()
        {
            ConfigurationRetriever = new Mock<IConfigurationRetriever<OpenIdConnectConfiguration>>();
            OidcConfigManager = new Mock<ConfigurationManager<OpenIdConnectConfiguration>>(
                EntraIdTokenPolicyValidator.MetadataAddress,
                ConfigurationRetriever.Object);
            JsonWebTokenHandler = new Mock<JsonWebTokenHandler>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            FeatureFlagService = new Mock<IFeatureFlagService>();
            FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(It.IsAny<User>())).Returns(true);
            FeatureFlagService.Setup(x => x.CanUseAzureDevOpsCredentials(It.IsAny<User>())).Returns(true);

            TenantId = new Guid("c311b905-19a2-483e-a014-41d0fcdc99cf");
            ObjectId = new Guid("d17083b8-74e0-46c6-b69f-764da2e6fc0e");
            Issuer = $"https://login.microsoftonline.com/{TenantId}/v2.0";
            AllowedTenantIds = [TenantId.ToString()];

            PackageOwner = new User("test-user");

            Configuration.Setup(x => x.EntraIdAudience).Returns("nuget-audience");
            Configuration.Setup(x => x.AllowedEntraIdTenants).Returns(() => AllowedTenantIds);

            Target = new EntraIdTokenPolicyValidator(
                OidcConfigManager.Object,
                Configuration.Object,
                FeatureFlagService.Object,
                JsonWebTokenHandler.Object);
        }

        public EntraIdTokenPolicyValidator Target { get; }
        public Mock<IConfigurationRetriever<OpenIdConnectConfiguration>> ConfigurationRetriever { get; }
        public Mock<ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigManager { get; }
        public Mock<JsonWebTokenHandler> JsonWebTokenHandler { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public Mock<IFeatureFlagService> FeatureFlagService { get; }
        public Guid TenantId { get; }
        public Guid ObjectId { get; }
        public string Issuer { get; set; }
        public string[] AllowedTenantIds { get; set; }
        public User PackageOwner { get; }

        public JsonWebToken Token
        {
            get
            {
                var handler = new JsonWebTokenHandler();
                return handler.ReadJsonWebToken(handler.CreateToken(new SecurityTokenDescriptor
                {
                    Claims = new Dictionary<string, object>
                    {
                        { "tid", TenantId.ToString() },
                        { "iss", Issuer },
                        { "uti", "test-token-id" },
                    }
                }));
            }
        }

        public JsonWebKey JsonWebKey => new() { Kid = "key-id", AdditionalData = { { "issuer", Issuer } } };
        public OpenIdConnectConfiguration OpenIdConnectConfiguration => new() { JsonWebKeySet = new JsonWebKeySet { Keys = { JsonWebKey } } };

        private JsonWebToken CreateToken(Dictionary<string, object> claims)
        {
            var handler = new JsonWebTokenHandler();
            return handler.ReadJsonWebToken(handler.CreateToken(new SecurityTokenDescriptor
            {
                Claims = claims
            }));
        }
    }
}
