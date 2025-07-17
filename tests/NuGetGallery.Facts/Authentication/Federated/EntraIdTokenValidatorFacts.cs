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
        public class TheIsTenantAllowedMethod : EntraIdTokenValidatorFacts
        {
            [Fact]
            public void AllowsTenantIdWhenInAllowList()
            {
                // Act
                var allowed = Target.IsTenantAllowed(new Guid(AllowedTenantIds[0]));

                // Assert
                Assert.True(allowed);
            }

            [Fact]
            public void RejectsTenantIdWhenInAllowList()
            {
                // Act
                var allowed = Target.IsTenantAllowed(new Guid("b3ad8ee4-f667-4a19-9091-206ef363beb1"));

                // Assert
                Assert.False(allowed);
            }

            [Fact]
            public void AllowsTenantIdWhenAllAreAllowed()
            {
                // Arrange
                AllowedTenantIds[0] = "all";

                // Act
                var allowed = Target.IsTenantAllowed(new Guid("b3ad8ee4-f667-4a19-9091-206ef363beb1"));

                // Assert
                Assert.True(allowed);
            }

            [Fact]
            public void AllTenantIdsAreNotAllowedWhenAllIsNotOnlyArrayItem()
            {
                // Arrange
                AllowedTenantIds = ["all", "c311b905-19a2-483e-a014-41d0fcdc99cf"];

                // Act
                var allowed = Target.IsTenantAllowed(new Guid("b3ad8ee4-f667-4a19-9091-206ef363beb1"));

                // Assert
                Assert.False(allowed);
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
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal($"The JSON web token is missing the {claim} claim.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsInvalidCredentialType()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The JSON web token must have an azpacr claim with a value of 2.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsInvalidIdentityType()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The JSON web token must have an idtyp claim with a value of app.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsInvalidVersion()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The JSON web token must have a ver claim with a value of 2.0.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsOidNotMatchingSub()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The JSON web token sub claim must match the oid claim.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsWrongTenantId()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The JSON web token must have a tid claim that matches the policy.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsNotAllowedTenantId()
            {
                // Arrange
                AllowedTenantIds = ["different-tenant-id"];

                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The tenant ID in the JSON web token is not in allow list.", result.InternalReason);
            }

            [Fact]
            public async Task RejectsWrongObjectId()
            {
                // Arrange
                var differentObjectId = new Guid("d8f0bfc3-5def-4079-b08c-618832b6ae16");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
                Assert.Equal("The JSON web token must have a oid claim that matches the policy.", result.InternalReason);
            }

            [Fact]
            public async Task ReturnsSuccessForValidPolicy()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
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
            public async Task RejectsInvalidCriteriaJson()
            {
                // Arrange
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = "invalid"
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

            TenantId = new Guid("c311b905-19a2-483e-a014-41d0fcdc99cf");
            ObjectId = new Guid("d17083b8-74e0-46c6-b69f-764da2e6fc0e");
            Issuer = $"https://login.microsoftonline.com/{TenantId}/v2.0";
            AllowedTenantIds = [TenantId.ToString()];

            Configuration.Setup(x => x.EntraIdAudience).Returns("nuget-audience");
            Configuration.Setup(x => x.AllowedEntraIdTenants).Returns(() => AllowedTenantIds);

            Target = new EntraIdTokenPolicyValidator(
                OidcConfigManager.Object,
                Configuration.Object,
                JsonWebTokenHandler.Object);
        }

        public EntraIdTokenPolicyValidator Target { get; }
        public Mock<IConfigurationRetriever<OpenIdConnectConfiguration>> ConfigurationRetriever { get; }
        public Mock<ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigManager { get; }
        public Mock<JsonWebTokenHandler> JsonWebTokenHandler { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public Guid TenantId { get; }
        public Guid ObjectId { get; }
        public string Issuer { get; set; }
        public string[] AllowedTenantIds { get; set; }

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
