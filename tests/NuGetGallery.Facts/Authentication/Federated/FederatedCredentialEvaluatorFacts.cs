// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NuGet.Services.Entities;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class FederatedCredentialEvaluatorFacts
    {
        public class TheGetMatchingPolicyAsyncMethod : FederatedCredentialEvaluatorFacts
        {
            [Fact]
            public async Task ReturnsNoMatchingPolicyWhenNoneAreProvided()
            {
                // Act
                var evaluation = await Target.GetMatchingPolicyAsync([], BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Empty(evaluation.Results);
            }

            [Fact]
            public async Task RejectsUnknownCredentialType()
            {
                // Arrange
                Policies[0].Type = (FederatedCredentialType)int.MaxValue;

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                var result = Assert.Single(evaluation.Results);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, result.Type);
                Assert.Equal("The policy type does not match the token issuer.", result.InternalReason);
            }

            /// <summary>
            /// This property should be set by the caller (such as when the API key is created).
            /// </summary>
            [Fact]
            public async Task DoesNotSetLastMatchedOnPolicy()
            {
                // Act
                await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Null(Policies[0].LastMatched);
            }

            [Fact]
            public async Task DoesNotSetLastMatchedWhenNoPolicyIsMatched()
            {
                // Act
                await Target.GetMatchingPolicyAsync(Policies, "bad token");

                // Assert
                Assert.All(Policies, x => Assert.Null(x.LastMatched));
            }

            [Fact]
            public async Task PrefersOlderMatchingPolicy()
            {
                // Arrange
                var newerPolicy = new FederatedCredentialPolicy
                {
                    Key = 24,
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    Created = Policies[0].Created.AddDays(1),
                };
                Policies.Insert(0, newerPolicy);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.MatchedPolicy, evaluation.Type);
                Assert.Same(Policies[1], evaluation.MatchedPolicy);
                Assert.Equal(23, evaluation.FederatedCredential.FederatedCredentialPolicyKey);
                var result = Assert.Single(evaluation.Results);
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                Assert.Same(evaluation.MatchedPolicy, result.Policy);
                Assert.Same(evaluation.FederatedCredential, result.FederatedCredential);
            }

            [Fact]
            public async Task RejectsInvalidTokenFormat()
            {
                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, "bad token");

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal("The bearer token could not be parsed as a JSON web token.", evaluation.UserError);
            }


            [Theory]
            [InlineData("aud", "The JSON web token must have exactly one aud claim value.")]
            [InlineData("iss", "The JSON web token must have an iss claim.")]
            public async Task RejectsMissingClaim(string claim, string userError)
            {
                // Arrange
                Claims.Remove(claim);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);
            }

            [Theory]
            [InlineData("aud", "  ", "The JSON web token must have an aud claim.")]
            [InlineData("iss", "  ", "The JSON web token must have an iss claim.")]
            [InlineData("iss", "foo", "The JSON web token iss claim must be a valid HTTPS URL.")]
            [InlineData("iss", "http://login.microsoftonline.com/c311b905-19a2-483e-a014-41d0fcdc99cf/v2.0", "The JSON web token iss claim must be a valid HTTPS URL.")]
            [InlineData("iss", "https://localhost/custom", "The JSON web token iss claim is not supported.")]
            public async Task RejectsInvalidClaim(string claim, object value, string userError)
            {
                // Arrange
                Claims[claim] = value;

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);
            }

            [Fact]
            public async Task RejectsMultipleAudiences()
            {
                // Arrange
                Claims["aud"] = new[] { "nuget.org", "microsoft.com" };

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal("The JSON web token must have exactly one aud claim value.", evaluation.UserError);
            }
        }

        public class EntraId : FederatedCredentialEvaluatorFacts
        {
            [Fact]
            public async Task RejectsTokenWhenEntraIdEvaluatorRejectsIt()
            {
                // Arrange
                EntraIdTokenResult.IsValid = false;

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal("The JSON web token could not be validated.", evaluation.UserError);
            }

            [Theory]
            [InlineData(typeof(SecurityTokenExpiredException), "The JSON web token has expired.")]
            [InlineData(typeof(SecurityTokenInvalidAudienceException), "The JSON web token has an incorrect audience.")]
            [InlineData(typeof(SecurityTokenInvalidSignatureException), "The JSON web token has an invalid signature.")]
            public async Task ProvidesSpecificErrorMessageForKnownValidationException(Type exceptionType, string userError)
            {
                // Arrange
                EntraIdTokenResult.IsValid = false;
                EntraIdTokenResult.Exception = (Exception)Activator.CreateInstance(exceptionType);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);
            }

            [Fact]
            public async Task UsesDefaultMessageForUnknownValidationException()
            {
                // Arrange
                EntraIdTokenResult.IsValid = false;
                EntraIdTokenResult.Exception = new InvalidOperationException("I'm sorry, Dave. I'm afraid I can't do that.");

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal("The JSON web token could not be validated.", evaluation.UserError);
            }

            [Theory]
            [InlineData("uti", "The JSON web token must have a uti claim.")]
            public async Task RejectsMissingClaim(string claim, string userError)
            {
                // Arrange
                Claims.Remove(claim);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);
            }

            [Theory]
            [InlineData("uti", "  ", "The JSON web token must have a uti claim.")]
            public async Task RejectsInvalidClaim(string claim, object value, string userError)
            {
                // Arrange
                Claims[claim] = value;

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);
            }
        }

        public class EntraIdServicePrincipal : FederatedCredentialEvaluatorFacts
        {
            [Theory]
            [InlineData("tid")]
            [InlineData("oid")]
            [InlineData("azpacr")]
            [InlineData("idtyp")]
            [InlineData("ver")]
            public async Task RejectsMissingClaim(string claim)
            {
                // Arrange
                Claims.Remove(claim);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task RejectsInvalidCredentialType()
            {
                // Arrange
                Claims["azpacr"] = "1";

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task RejectsInvalidIdentityType()
            {
                // Arrange
                Claims["idtyp"] = "app+user";

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task RejectsInvalidVersion()
            {
                // Arrange
                Claims["ver"] = "1.0";

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task RejectsOidNotMatchingSub()
            {
                // Arrange
                Claims["sub"] = "my-client";

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task RejectsWrongTenantId()
            {
                // Arrange
                Claims["tid"] = "d8f0bfc3-5def-4079-b08c-618832b6ae16";

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task RejectsWrongObjectId()
            {
                // Arrange
                Claims["oid"] = "d8f0bfc3-5def-4079-b08c-618832b6ae16";
                Claims["sub"] = "d8f0bfc3-5def-4079-b08c-618832b6ae16";

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, evaluation.Type);
                Assert.Equal(FederatedCredentialPolicyResultType.Unauthorized, Assert.Single(evaluation.Results).Type);
            }

            [Fact]
            public async Task GeneratesCredentialForMatchingPolicy()
            {
                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken);

                // Assert
                Assert.Equal(EvaluatedFederatedCredentialPoliciesType.MatchedPolicy, evaluation.Type);

                Assert.Same(Policies[0], evaluation.MatchedPolicy);
                var result = Assert.Single(evaluation.Results);
                Assert.Equal(FederatedCredentialPolicyResultType.Success, result.Type);
                Assert.Same(evaluation.MatchedPolicy, result.Policy);
                Assert.Same(evaluation.FederatedCredential, result.FederatedCredential);

                Assert.NotNull(evaluation.FederatedCredential);
                Assert.Equal("fate's wink", evaluation.FederatedCredential.Identity);
                Assert.Equal(23, evaluation.FederatedCredential.FederatedCredentialPolicyKey);
                Assert.Equal(FederatedCredentialType.EntraIdServicePrincipal, evaluation.FederatedCredential.Type);
                Assert.Equal(UtcNow, evaluation.FederatedCredential.Created);
                Assert.Equal(Expires, evaluation.FederatedCredential.Expires);
            }
        }

        public FederatedCredentialEvaluatorFacts()
        {
            EntraIdTokenValidator = new Mock<IEntraIdTokenValidator>();
            DateTimeProvider = new Mock<IDateTimeProvider>();
            Logger = new Mock<ILogger<FederatedCredentialEvaluator>>();

            TenantId = new Guid("c311b905-19a2-483e-a014-41d0fcdc99cf");
            ObjectId = new Guid("d17083b8-74e0-46c6-b69f-764da2e6fc0e");
            Claims = new Dictionary<string, object>
            {
                { "aud", "nuget.org" },
                { "iss", $"https://login.microsoftonline.com/{TenantId}/v2.0" },
                { "tid", TenantId.ToString() },
                { "oid", ObjectId.ToString() },
                { "sub", ObjectId.ToString() },
                { "uti", "fate's wink" },
                { "azpacr", "2" },
                { "idtyp", "app" },
                { "ver", "2.0" },
            };
            Policies = new List<FederatedCredentialPolicy>
            {
                new FederatedCredentialPolicy
                {
                    Key = 23,
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    Created = new DateTime(2024, 9, 10, 11, 12, 13, DateTimeKind.Utc),
                }
            };
            EntraIdTokenResult = new TokenValidationResult { IsValid = true };
            UtcNow = new DateTimeOffset(2024, 10, 10, 13, 35, 0, TimeSpan.Zero);
            Expires = new DateTimeOffset(2024, 10, 11, 0, 0, 0, TimeSpan.Zero);

            EntraIdTokenValidator
                .Setup(x => x.ValidateAsync(It.IsAny<JsonWebToken>()))
                .ReturnsAsync(() => EntraIdTokenResult);
            DateTimeProvider
                .Setup(x => x.UtcNow)
                .Returns(() => UtcNow.UtcDateTime);

            Target = new FederatedCredentialEvaluator(
                EntraIdTokenValidator.Object,
                DateTimeProvider.Object,
                Logger.Object);
        }

        public Mock<IEntraIdTokenValidator> EntraIdTokenValidator { get; }
        public Mock<IDateTimeProvider> DateTimeProvider { get; }
        public Mock<ILogger<FederatedCredentialEvaluator>> Logger { get; }
        public Guid TenantId { get; }
        public Guid ObjectId { get; }
        public Dictionary<string, object> Claims { get; }
        public List<FederatedCredentialPolicy> Policies { get; }
        public TokenValidationResult EntraIdTokenResult { get; }
        public DateTimeOffset UtcNow { get; }
        public DateTimeOffset Expires { get; }

        public string BearerToken => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Claims = Claims, Expires = Expires.UtcDateTime });

        public FederatedCredentialEvaluator Target { get; }
    }
}
