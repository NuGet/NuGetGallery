// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class EntraIdTokenValidatorFacts
    {
        public class TheValidateAsyncMethod : EntraIdTokenValidatorFacts
        {
            [Fact]
            public async Task RejectsMissingAudience()
            {
                // Arrange
                Configuration.Setup(x => x.EntraIdAudience).Returns((string?)null);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ValidateAsync(Token));
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
                var actual = await Target.ValidateAsync(Token);

                // Assert
                Assert.Same(result, actual);
            }

            [Fact]
            public async Task ConfiguresTokenValidationParameters()
            {
                // Arrange
                var token = Token;

                // Act
                await Target.ValidateAsync(token);

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
                await Target.ValidateAsync(Token);
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
                await Target.ValidateAsync(Token);
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
                await Target.ValidateAsync(Token);
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
                await Target.ValidateAsync(Token);
                JsonWebTokenHandler.Verify(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>(), It.IsAny<TokenValidationParameters>()), Times.Once);
                var invocation = Assert.Single(JsonWebTokenHandler.Invocations);
                var tokenValidationParameters = (TokenValidationParameters)invocation.Arguments[1];
                var key = new JsonWebKey { Kid = "key-id", AdditionalData = { { "issuer", "https://localhost/my-issuer" } } };
                var config = new OpenIdConnectConfiguration { JsonWebKeySet = new JsonWebKeySet { Keys = { key } } };

                // Act & Assert
                Assert.Throws<SecurityTokenInvalidIssuerException>(
                    () => tokenValidationParameters.IssuerSigningKeyValidatorUsingConfiguration(key, Token, tokenValidationParameters, config));
            }
        }

        public EntraIdTokenValidatorFacts()
        {
            ConfigurationRetriever = new Mock<IConfigurationRetriever<OpenIdConnectConfiguration>>();
            OidcConfigManager = new Mock<ConfigurationManager<OpenIdConnectConfiguration>>(
                EntraIdTokenValidator.MetadataAddress,
                ConfigurationRetriever.Object);
            JsonWebTokenHandler = new Mock<JsonWebTokenHandler>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            Configuration.Setup(x => x.EntraIdAudience).Returns("nuget-audience");

            TenantId = "c311b905-19a2-483e-a014-41d0fcdc99cf";
            Issuer = $"https://login.microsoftonline.com/{TenantId}/v2.0";

            Target = new EntraIdTokenValidator(
                OidcConfigManager.Object,
                JsonWebTokenHandler.Object,
                Configuration.Object);
        }

        public EntraIdTokenValidator Target { get; }
        public Mock<IConfigurationRetriever<OpenIdConnectConfiguration>> ConfigurationRetriever { get; }
        public Mock<ConfigurationManager<OpenIdConnectConfiguration>> OidcConfigManager { get; }
        public Mock<JsonWebTokenHandler> JsonWebTokenHandler { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public string TenantId { get; }
        public string Issuer { get; set; }

        public JsonWebToken Token
        {
            get
            {
                var handler = new JsonWebTokenHandler();
                return handler.ReadJsonWebToken(handler.CreateToken(new SecurityTokenDescriptor
                {
                    Claims = new Dictionary<string, object>
                    {
                        { "tid", TenantId },
                        { "iss", Issuer },
                    }
                }));
            }
        }

        public JsonWebKey JsonWebKey => new() { Kid = "key-id", AdditionalData = { { "issuer", Issuer } } };
        public OpenIdConnectConfiguration OpenIdConnectConfiguration => new() { JsonWebKeySet = new JsonWebKeySet { Keys = { JsonWebKey } } };
    }
}
