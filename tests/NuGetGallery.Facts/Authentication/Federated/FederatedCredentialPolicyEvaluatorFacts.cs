// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class FederatedCredentialPolicyEvaluatorFacts
    {
        public class TheValidatePolicyMethod : FederatedCredentialPolicyEvaluatorFacts
        {
            [Fact]
            public void DelegatesToEntraIdValidatorForEntraIdServicePrincipalPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                var expectedResult = FederatedCredentialPolicyValidationResult.Success(policy);
                var entraIdValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.EntraId);
                entraIdValidator.Setup(x => x.ValidatePolicy(policy)).Returns(expectedResult);

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Same(expectedResult, result);
                entraIdValidator.Verify(x => x.ValidatePolicy(policy), Times.Once);
            }

            [Fact]
            public void DelegatesToGitHubValidatorForGitHubActionsPolicy()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    PackageOwner = user,
                    Criteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml"}"""
                };

                var expectedResult = FederatedCredentialPolicyValidationResult.Success(policy);
                var gitHubValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.GitHubActions);
                gitHubValidator.Setup(x => x.ValidatePolicy(policy)).Returns(expectedResult);

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Same(expectedResult, result);
                gitHubValidator.Verify(x => x.ValidatePolicy(policy), Times.Once);
            }

            [Fact]
            public void ThrowsForUnsupportedPolicyType()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = (FederatedCredentialType)999, // Unsupported type
                    PackageOwner = user,
                    Criteria = "dummy"
                };

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => Target.ValidatePolicy(policy));
                Assert.Contains("Unsupported", exception.Message);
                Assert.Contains("999", exception.Message);
            }

            [Fact]
            public void ThrowsWhenNoValidatorFoundForIssuerType()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                // Create a new evaluator without the EntraId validator
                var evaluatorWithoutEntraId = new FederatedCredentialPolicyEvaluator(
                    TokenValidators.Where(v => v.Object.IssuerType != FederatedCredentialIssuerType.EntraId).Select(x => x.Object).ToList(),
                    AdditionalValidators.Select(x => x.Object).ToList(),
                    AuditingService.Object,
                    DateTimeProvider.Object,
                    Logger.Object);

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => evaluatorWithoutEntraId.ValidatePolicy(policy));
                Assert.Contains("No validator found for issuer type", exception.Message);
                Assert.Contains("EntraId", exception.Message);
            }

            [Fact]
            public void CorrectlyMapsEntraIdServicePrincipalToEntraIdIssuerType()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                var entraIdValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.EntraId);
                var gitHubValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.GitHubActions);

                // Act
                Target.ValidatePolicy(policy);

                // Assert
                entraIdValidator.Verify(x => x.ValidatePolicy(policy), Times.Once);
                gitHubValidator.Verify(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()), Times.Never);
            }

            [Fact]
            public void CorrectlyMapsGitHubActionsToGitHubActionsIssuerType()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    PackageOwner = user,
                    Criteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml"}"""
                };

                var entraIdValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.EntraId);
                var gitHubValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.GitHubActions);

                // Act
                Target.ValidatePolicy(policy);

                // Assert
                gitHubValidator.Verify(x => x.ValidatePolicy(policy), Times.Once);
                entraIdValidator.Verify(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()), Times.Never);
            }

            [Fact]
            public void ReturnsValidationResultFromValidator()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    PackageOwner = user,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId))
                };

                var validationResult = FederatedCredentialPolicyValidationResult.BadRequest("Test error", "TestProperty");
                var entraIdValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.EntraId);
                entraIdValidator.Setup(x => x.ValidatePolicy(policy)).Returns(validationResult);

                // Act
                var result = Target.ValidatePolicy(policy);

                // Assert
                Assert.Same(validationResult, result);
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("Test error", result.UserMessage);
                Assert.Equal("TestProperty", result.PolicyPropertyName);
            }

            [Fact]
            public void PassesPolicyObjectToValidator()
            {
                // Arrange
                var user = new User("test-user");
                var policy = new FederatedCredentialPolicy
                {
                    Type = FederatedCredentialType.GitHubActions,
                    PackageOwner = user,
                    PolicyName = "Test Policy",
                    Criteria = """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml"}""",
                    Key = 42
                };

                var gitHubValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.GitHubActions);

                // Act
                Target.ValidatePolicy(policy);

                // Assert
                gitHubValidator.Verify(x => x.ValidatePolicy(It.Is<FederatedCredentialPolicy>(p =>
                    p.Type == FederatedCredentialType.GitHubActions &&
                    p.PackageOwner == user &&
                    p.PolicyName == "Test Policy" &&
                    p.Criteria == """{"owner":"test-owner","repository":"test-repo","workflow":"test.yml"}""" &&
                    p.Key == 42
                )), Times.Once);
            }
        }

        public class TheGetMatchingPolicyAsyncMethod : FederatedCredentialPolicyEvaluatorFacts
        {
            [Fact]
            public async Task ReturnsNoMatchingPolicyWhenNoneAreProvided()
            {
                // Act
                var evaluation = await Target.GetMatchingPolicyAsync([], BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.NoMatchingPolicy, evaluation.Type);

                AssertNoPoliciesCredentialAudit();
            }

            /// <summary>
            /// This property should be set by the caller (such as when the API key is created).
            /// </summary>
            [Fact]
            public async Task DoesNotSetLastMatchedOnPolicy()
            {
                // Act
                await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Null(Policies[0].LastMatched);
            }

            [Fact]
            public async Task DoesNotSetLastMatchedWhenNoPolicyIsMatched()
            {
                // Act
                await Target.GetMatchingPolicyAsync(Policies, "bad token", RequestHeaders);

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
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.MatchedPolicy, evaluation.Type);
                Assert.Same(Policies[1], evaluation.MatchedPolicy);
                Assert.Equal(23, evaluation.FederatedCredential.FederatedCredentialPolicyKey);
                AssertValidCredentialAudits(matchedPolicy: true);
            }

            [Fact]
            public async Task RejectsInvalidTokenFormat()
            {
                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, "bad token", RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("The bearer token could not be parsed as a JSON web token.", evaluation.UserError);

                AssertInvalidCredentialAudit();
            }

            [Theory]
            [InlineData("aud", "The JSON web token must have exactly one aud claim value.")]
            [InlineData("iss", "The JSON web token must have an iss claim.")]
            public async Task RejectsMissingClaim(string claim, string userError)
            {
                // Arrange
                Claims.Remove(claim);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);

                AssertInvalidCredentialAudit();
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
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal(userError, evaluation.UserError);

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task RejectsMultipleAudiences()
            {
                // Arrange
                Claims["aud"] = new[] { "nuget.org", "microsoft.com" };

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("The JSON web token must have exactly one aud claim value.", evaluation.UserError);

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task RejectsInvalidBearerTokenWhenAdditionalValidatorPasses()
            {
                // Arrange
                AdditionalValidatorA
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Valid);

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, "bad token", RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("The bearer token could not be parsed as a JSON web token.", evaluation.UserError);

                Assert.Single(AdditionalValidatorA.Invocations);
                Assert.Single(AdditionalValidatorB.Invocations);
                Assert.Single(Logger.Invocations.Where(x => x.Arguments[0].Equals(LogLevel.Warning)));

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task RejectsValidBearerTokenWhenAdditionalValidatorFails()
            {
                // Arrange
                AdditionalValidatorA
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized("Not gonna work bruv"));

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("Not gonna work bruv", evaluation.UserError);

                Assert.Single(AdditionalValidatorA.Invocations);
                Assert.Single(AdditionalValidatorB.Invocations);
                Assert.Single(Logger.Invocations.Where(x => x.Arguments[0].Equals(LogLevel.Information)));

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task RejectsInvalidBearerTokenWhenAdditionalValidatorFails()
            {
                // Arrange
                AdditionalValidatorA
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized("Not gonna work bruv"));
                AdditionalValidatorB
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized(userError: null));

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, "bad token", RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("The bearer token could not be parsed as a JSON web token.", evaluation.UserError);

                Assert.Single(AdditionalValidatorA.Invocations);
                Assert.Single(AdditionalValidatorB.Invocations);
                Assert.DoesNotContain(Logger.Invocations, x => x.Arguments[0].Equals(LogLevel.Warning));

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task UsesFirstNonNullUserErrorFromAdditionalValidators()
            {
                // Arrange
                AdditionalValidatorA
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized(userError: null));
                AdditionalValidatorB
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized(userError: "Not gonna work bruv"));

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("Not gonna work bruv", evaluation.UserError);

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task PrefersFirstUserErrorFromAdditionalValidators()
            {
                // Arrange
                AdditionalValidatorA
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized(userError: "A"));
                AdditionalValidatorB
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized(userError: "B"));

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("A", evaluation.UserError);

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task UsesGenericUserErrorWhenAdditionalValidatorFails()
            {
                // Arrange
                AdditionalValidatorA
                    .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                    .ReturnsAsync(FederatedCredentialValidation.Unauthorized(userError: null));

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.BadToken, evaluation.Type);
                Assert.Equal("The request could not be authenticated.", evaluation.UserError);

                AssertInvalidCredentialAudit();
            }

            [Fact]
            public async Task GeneratesCredentialForMatchingPolicy()
            {
                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.MatchedPolicy, evaluation.Type);

                Assert.Same(Policies[0], evaluation.MatchedPolicy);

                Assert.NotNull(evaluation.FederatedCredential);
                Assert.Equal("fate's wink", evaluation.FederatedCredential.Identity);
                Assert.Equal(23, evaluation.FederatedCredential.FederatedCredentialPolicyKey);
                Assert.Equal(FederatedCredentialType.EntraIdServicePrincipal, evaluation.FederatedCredential.Type);
                Assert.Equal(UtcNow, evaluation.FederatedCredential.Created);
                Assert.Equal(Expires, evaluation.FederatedCredential.Expires);

                // Verify that policy evaluation was logged
                Assert.Contains(Logger.Invocations, invocation =>
                    invocation.Arguments[0].Equals(LogLevel.Information) &&
                    invocation.Arguments[2].ToString()!.StartsWith("Evaluated policy"));

                AssertValidCredentialAudits(matchedPolicy: true);
            }

            [Fact]
            public async Task ContinuesProcessingWhenTokenValidatorThrowsExceptionDuringPolicyEvaluation()
            {
                // Arrange
                var failingPolicy = new FederatedCredentialPolicy
                {
                    Key = 1,
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    Created = Policies[0].Created.AddDays(-1), // Make this older so it's evaluated first
                    CreatedBy = new User { Username = "creator" },
                    PackageOwner = new User { Username = "owner" },
                };

                var successPolicy = new FederatedCredentialPolicy
                {
                    Key = 2,
                    Type = FederatedCredentialType.EntraIdServicePrincipal,
                    Criteria = JsonSerializer.Serialize(new EntraIdServicePrincipalCriteria(TenantId, ObjectId)),
                    Created = Policies[0].Created, // Same as original policy
                    CreatedBy = new User { Username = "creator" },
                    PackageOwner = new User { Username = "owner" },
                };

                var policiesWithFailure = new List<FederatedCredentialPolicy> { failingPolicy, successPolicy };

                // Setup the first validator to throw an exception for the first policy
                var entraIdValidator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.EntraId);
                entraIdValidator
                    .Setup(x => x.EvaluatePolicyAsync(It.Is<FederatedCredentialPolicy>(p => p.Key == failingPolicy.Key), It.IsAny<JsonWebToken>()))
                    .Throws(new InvalidOperationException("Lorem ipsum"));

                // Setup the same validator to succeed for the second policy
                entraIdValidator
                    .Setup(x => x.EvaluatePolicyAsync(It.Is<FederatedCredentialPolicy>(p => p.Key == successPolicy.Key), It.IsAny<JsonWebToken>()))
                    .Returns(Task.FromResult(FederatedCredentialPolicyResult.Success));

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(policiesWithFailure, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.MatchedPolicy, evaluation.Type);
                Assert.Same(successPolicy, evaluation.MatchedPolicy);
                Assert.Equal(2, evaluation.FederatedCredential.FederatedCredentialPolicyKey);

                // Verify both policies were evaluated despite the first one throwing an exception
                entraIdValidator.Verify(x => x.EvaluatePolicyAsync(It.Is<FederatedCredentialPolicy>(p => p.Key == failingPolicy.Key), It.IsAny<JsonWebToken>()), Times.Once);
                entraIdValidator.Verify(x => x.EvaluatePolicyAsync(It.Is<FederatedCredentialPolicy>(p => p.Key == successPolicy.Key), It.IsAny<JsonWebToken>()), Times.Once);

                // Verify that the exception was logged appropriately
                Assert.Contains(Logger.Invocations, invocation =>
                    invocation.Arguments[0].Equals(LogLevel.Information) &&
                    invocation.Arguments[2].ToString()!.Contains("Evaluated policy key 1") &&
                    invocation.Arguments[2].ToString()!.Contains("Unauthorized") &&
                    invocation.Arguments[2].ToString()!.Contains("Lorem ipsum"));
            }

            [Theory]
            [InlineData(0)]
            [InlineData(1)]
            [InlineData(int.MaxValue)]
            public async Task NoMatchingPolicy_ReturnsFirstDisclosableError(int firstDisclosableError)
            {
                // Arrange
                var user = new User { Username = "user" };
                var policies = new List<FederatedCredentialPolicy>
                {
                    new FederatedCredentialPolicy{Key = 0, CreatedBy = user, PackageOwner = user, Type = FederatedCredentialType.EntraIdServicePrincipal},
                    new FederatedCredentialPolicy{Key = 1, CreatedBy = user, PackageOwner = user, Type = FederatedCredentialType.EntraIdServicePrincipal},
                    new FederatedCredentialPolicy{Key = 2, CreatedBy = user, PackageOwner = user, Type = FederatedCredentialType.EntraIdServicePrincipal},
                };

                var validator = TokenValidators.First(v => v.Object.IssuerType == FederatedCredentialIssuerType.EntraId);

                // Setup policies to return errors based on the test parameter
                for (int i = 0; i < policies.Count; i++)
                {
                    var evaluationResult = FederatedCredentialPolicyResult.Unauthorized(
                        $"Error #{i}", isErrorDisclosable: i >= firstDisclosableError);

                    validator
                        .Setup(x => x.EvaluatePolicyAsync(policies[i], It.IsAny<JsonWebToken>()))
                        .Returns(Task.FromResult(evaluationResult));
                }

                // Act
                var evaluation = await Target.GetMatchingPolicyAsync(policies, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(OidcTokenEvaluationResultType.NoMatchingPolicy, evaluation.Type);
                if (firstDisclosableError < policies.Count)
                {
                    Assert.Equal($"Error #{firstDisclosableError}", evaluation.UserError);
                }
                else
                {
                    Assert.Null(evaluation.UserError);
                }
            }
        }

        public FederatedCredentialPolicyEvaluatorFacts()
        {
            TokenValidators = new List<Mock<ITokenPolicyValidator>>();
            AdditionalValidators = new List<Mock<IFederatedCredentialValidator>>();
            AuditingService = new Mock<IAuditingService>();
            DateTimeProvider = new Mock<IDateTimeProvider>();
            Logger = new Mock<ILogger<FederatedCredentialPolicyEvaluator>>();
            AdditionalValidatorA = new Mock<IFederatedCredentialValidator>();
            AdditionalValidatorB = new Mock<IFederatedCredentialValidator>();
            AdditionalValidators.Add(AdditionalValidatorA);
            AdditionalValidators.Add(AdditionalValidatorB);

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
                    CreatedBy = new User { Username = "creator" },
                    PackageOwner = new User { Username = "owner" },
                }
            };
            UtcNow = new DateTimeOffset(2024, 10, 10, 13, 35, 0, TimeSpan.Zero);
            Expires = new DateTimeOffset(2024, 10, 11, 0, 0, 0, TimeSpan.Zero);
            RequestHeaders = new NameValueCollection();

            // Setup token validators
            var entraIdTokenValidator = new Mock<ITokenPolicyValidator>();
            entraIdTokenValidator.Setup(x => x.IssuerAuthority).Returns(EntraIdTokenPolicyValidator.Authority);
            entraIdTokenValidator.Setup(x => x.IssuerType).Returns(FederatedCredentialIssuerType.EntraId);
            entraIdTokenValidator.Setup(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>()))
                .ReturnsAsync(new TokenValidationResult { IsValid = true });
            entraIdTokenValidator.Setup(x => x.ValidateTokenIdentifier(It.IsAny<JsonWebToken>()))
                .Returns(("fate's wink", null));
            entraIdTokenValidator.Setup(x => x.EvaluatePolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<JsonWebToken>()))
                .Returns(Task.FromResult(FederatedCredentialPolicyResult.Success));
            entraIdTokenValidator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                .Returns((FederatedCredentialPolicy p) => FederatedCredentialPolicyValidationResult.Success(p));
            TokenValidators.Add(entraIdTokenValidator);

            var gitHubTokenValidator = new Mock<ITokenPolicyValidator>();
            gitHubTokenValidator.Setup(x => x.IssuerAuthority).Returns(GitHubTokenPolicyValidator.Authority);
            gitHubTokenValidator.Setup(x => x.IssuerType).Returns(FederatedCredentialIssuerType.GitHubActions);
            gitHubTokenValidator.Setup(x => x.ValidateTokenAsync(It.IsAny<JsonWebToken>()))
                .ReturnsAsync(new TokenValidationResult { IsValid = true });
            gitHubTokenValidator.Setup(x => x.ValidateTokenIdentifier(It.IsAny<JsonWebToken>()))
                .Returns(("github-token-id", null));
            gitHubTokenValidator.Setup(x => x.EvaluatePolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<JsonWebToken>()))
                .Returns(Task.FromResult(FederatedCredentialPolicyResult.Success));
            gitHubTokenValidator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                .Returns((FederatedCredentialPolicy p) => FederatedCredentialPolicyValidationResult.Success(p));
            TokenValidators.Add(gitHubTokenValidator);

            DateTimeProvider
                .Setup(x => x.UtcNow)
                .Returns(() => UtcNow.UtcDateTime);
            AdditionalValidatorA
                .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                .ReturnsAsync(FederatedCredentialValidation.NotApplicable);
            AdditionalValidatorB
                .Setup(x => x.ValidateAsync(It.IsAny<NameValueCollection>(), It.IsAny<FederatedCredentialIssuerType>(), It.IsAny<IEnumerable<Claim>>()))
                .ReturnsAsync(FederatedCredentialValidation.NotApplicable);
        }

        public List<Mock<ITokenPolicyValidator>> TokenValidators { get; }
        public List<Mock<IFederatedCredentialValidator>> AdditionalValidators { get; }
        public Mock<IAuditingService> AuditingService { get; }
        public Mock<IDateTimeProvider> DateTimeProvider { get; }
        public Mock<ILogger<FederatedCredentialPolicyEvaluator>> Logger { get; }
        public Mock<IFederatedCredentialValidator> AdditionalValidatorA { get; }
        public Mock<IFederatedCredentialValidator> AdditionalValidatorB { get; }
        public Guid TenantId { get; }
        public Guid ObjectId { get; }
        public Dictionary<string, object> Claims { get; }
        public List<FederatedCredentialPolicy> Policies { get; }
        public DateTimeOffset UtcNow { get; }
        public DateTimeOffset Expires { get; }
        public NameValueCollection RequestHeaders { get; }

        public string BearerToken => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Claims = Claims, Expires = Expires.UtcDateTime });

        public FederatedCredentialPolicyEvaluator Target => new FederatedCredentialPolicyEvaluator(
            TokenValidators.Select(x => x.Object).ToList(),
            AdditionalValidators.Select(x => x.Object).ToList(),
            AuditingService.Object,
            DateTimeProvider.Object,
            Logger.Object);

        protected List<AuditRecord> AssertAuditResourceTypes(params string[] resourceTypeOrder)
        {
            var records = AuditingService
                .Invocations
                .Where(x => x.Method.Name == nameof(IAuditingService.SaveAuditRecordAsync))
                .Select(x => x.Arguments[0])
                .Cast<AuditRecord>()
                .ToList();
            Assert.Equal(resourceTypeOrder, records.Select(x => x.GetResourceType()).ToArray());
            return records;
        }

        private void AssertNoPoliciesCredentialAudit()
        {
            var audits = AssertAuditResourceTypes(ExternalSecurityTokenAuditRecord.ResourceType);
            var tokenAudit = Assert.IsType<ExternalSecurityTokenAuditRecord>(audits[0]);
            Assert.Equal(AuditedExternalSecurityTokenAction.Validated, tokenAudit.Action);
        }

        private void AssertInvalidCredentialAudit()
        {
            var audits = AssertAuditResourceTypes(ExternalSecurityTokenAuditRecord.ResourceType);
            var tokenAudit = Assert.IsType<ExternalSecurityTokenAuditRecord>(audits[0]);
            Assert.Equal(AuditedExternalSecurityTokenAction.Rejected, tokenAudit.Action);
        }

        private void AssertValidCredentialAudits(bool matchedPolicy)
        {
            var audits = AssertAuditResourceTypes(ExternalSecurityTokenAuditRecord.ResourceType, FederatedCredentialPolicyAuditRecord.ResourceType);
            var tokenAudit = Assert.IsType<ExternalSecurityTokenAuditRecord>(audits[0]);
            Assert.Equal(AuditedExternalSecurityTokenAction.Validated, tokenAudit.Action);
            var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[1]);
            Assert.Equal(AuditedFederatedCredentialPolicyAction.Compare, policyAudit.Action);
            Assert.Equal(matchedPolicy, policyAudit.Success);
        }
    }
}
