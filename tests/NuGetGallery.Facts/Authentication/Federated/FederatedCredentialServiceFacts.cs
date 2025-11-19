// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class FederatedCredentialServiceFacts
    {
        public class TheAddPolicyAsyncMethod : FederatedCredentialServiceFacts
        {
            [Fact]
            public async Task WhenCreatedByIsNull_ReturnsBadRequest()
            {
                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: null!,
                    packageOwner: "testowner",
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("The policy user is missing.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.CreatedBy), result.PolicyPropertyName);
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<bool>()), Times.Never);

                // Missing createdBy should not create an audit record as it is part of basic user input validation.
                AssertNoAudits();
            }

            [Fact]
            public async Task WhenPackageOwnerNotFound_ReturnsBadRequest()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                UserService.Setup(x => x.FindByUsername("nonexistent", false)).Returns((User)null!);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "nonexistent",
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("The policy package owner 'nonexistent' does not exist.", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.PackageOwner), result.PolicyPropertyName);
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<bool>()), Times.Never);

                // Missing policy owner should not create an audit record as it is part of basic user input validation.
                AssertNoAudits();
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public async Task WhenPackageOwnerIsNullOrWhitespace_ReturnsBadRequest(string? packageOwner)
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                UserService.Setup(x => x.FindByUsername(packageOwner, false)).Returns((User)null!);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: packageOwner!,
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Contains("does not exist", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.PackageOwner), result.PolicyPropertyName);

                // Missing policy owner should not create an audit record as it is part of basic user input validation.
                AssertNoAudits();
            }

            [Fact]
            public async Task WhenEvaluatorValidationFails_ReturnsValidationResultAndCreatesAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var validationResult = FederatedCredentialPolicyValidationResult.BadRequest(
                    "Invalid criteria format",
                    nameof(FederatedCredentialPolicy.Criteria));

                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(validationResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testowner",
                    criteria: """{"invalid": "json"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Same(validationResult, result);
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("Invalid criteria format", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<bool>()), Times.Never);
                AssertBadRequest(user, packageOwner, FederatedCredentialType.EntraIdServicePrincipal, """{"invalid": "json"}""", "Invalid criteria format");
            }

            [Fact]
            public async Task WhenPermissionValidationFails_ReturnsUnauthorizedAndCreatesAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(false);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testowner",
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Unauthorized, result.Type);
                Assert.Contains("does not have the required permissions", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.PackageOwner), result.PolicyPropertyName);
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<bool>()), Times.Never);
                AssertUnauthorized(user, packageOwner, FederatedCredentialType.EntraIdServicePrincipal, """{"test": "value"}""", result.UserMessage!);
            }

            [Fact]
            public async Task WhenValidInput_CreatesAndSavesPolicyWithCreateAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };
                var utcNow = new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc);

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);
                DateTimeProvider.Setup(x => x.UtcNow).Returns(utcNow);

                var successResult = FederatedCredentialPolicyValidationResult.Success(
                    new FederatedCredentialPolicy { Key = 42 });
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(successResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testowner",
                    criteria: """{"tenant":"test","object":"123"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.NotNull(result.Policy);

                // Verify policy properties were set correctly
                var savedPolicy = result.Policy;
                Assert.Equal("Test Policy", savedPolicy.PolicyName);
                Assert.Equal(utcNow, savedPolicy.Created);
                Assert.Same(user, savedPolicy.CreatedBy);
                Assert.Same(packageOwner, savedPolicy.PackageOwner);
                Assert.Equal(FederatedCredentialType.EntraIdServicePrincipal, savedPolicy.Type);
                Assert.Equal("""{"tenant":"test","object":"123"}""", savedPolicy.Criteria);

                // Verify policy was saved
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(
                    It.Is<FederatedCredentialPolicy>(p =>
                        p.PolicyName == "Test Policy" &&
                        p.CreatedBy == user &&
                        p.PackageOwner == packageOwner &&
                        p.Type == FederatedCredentialType.EntraIdServicePrincipal &&
                        p.Criteria == """{"tenant":"test","object":"123"}"""),
                    true),
                    Times.Once);

                // Verify CREATE audit record was created
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
            }

            [Fact]
            public async Task WhenPolicyNameIsNull_CreatesWithNullNameAndCreateAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var successResult = FederatedCredentialPolicyValidationResult.Success(
                    new FederatedCredentialPolicy { Key = 42 });
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(successResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testowner",
                    criteria: """{"test": "value"}""",
                    policyName: null,
                    policyType: FederatedCredentialType.GitHubActions);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Null(result.Policy.PolicyName);

                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(
                    It.Is<FederatedCredentialPolicy>(p => p.PolicyName == null),
                    true),
                    Times.Once);

                // Verify CREATE audit record was created
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
            }

            [Theory]
            [InlineData(FederatedCredentialType.EntraIdServicePrincipal)]
            [InlineData(FederatedCredentialType.GitHubActions)]
            public async Task WhenDifferentPolicyTypes_CreatesPolicyWithCorrectTypeAndCreateAuditRecord(FederatedCredentialType policyType)
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var successResult = FederatedCredentialPolicyValidationResult.Success(
                    new FederatedCredentialPolicy { Key = 42 });
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(successResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testowner",
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: policyType);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Equal(policyType, result.Policy.Type);

                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(
                    It.Is<FederatedCredentialPolicy>(p => p.Type == policyType),
                    true),
                    Times.Once);

                // Verify CREATE audit record was created
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
            }

            [Fact]
            public async Task WhenUserIsOrganization_ValidatesCorrectlyAndCreatesAuditRecord()
            {
                // Arrange
                var organization = new Organization { Key = 10, Username = "testorg" };
                var packageOwner = new User { Key = 20, Username = "testowner" };

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(organization, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var successResult = FederatedCredentialPolicyValidationResult.Success(
                    new FederatedCredentialPolicy { Key = 42 });
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(successResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: organization,
                    packageOwner: "testowner",
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Same(organization, result.Policy.CreatedBy);

                // Verify CREATE audit record was created
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
            }

            [Fact]
            public async Task WhenPackageOwnerIsOrganization_ValidatesCorrectlyAndCreatesAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var organization = new Organization { Key = 20, Username = "testorg" };

                UserService.Setup(x => x.FindByUsername("testorg", false)).Returns(organization);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var successResult = FederatedCredentialPolicyValidationResult.Success(
                    new FederatedCredentialPolicy { Key = 42 });
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(successResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testorg",
                    criteria: """{"test": "value"}""",
                    policyName: "Test Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Same(organization, result.Policy.PackageOwner);

                // Verify CREATE audit record was created
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
            }

            [Fact]
            public async Task WhenComplexCriteriaProvided_SavesCorrectlyAndCreatesAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };
                var complexCriteria = """{"tenant":"12345-abcd","objectId":"67890-efgh","environment":"production","workflow":"deploy.yml"}""";

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var successResult = FederatedCredentialPolicyValidationResult.Success(
                    new FederatedCredentialPolicy { Key = 42 });
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(successResult);

                // Act
                var result = await Target.AddPolicyAsync(
                    createdBy: user,
                    packageOwner: "testowner",
                    criteria: complexCriteria,
                    policyName: "Complex Policy",
                    policyType: FederatedCredentialType.EntraIdServicePrincipal);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Equal(complexCriteria, result.Policy.Criteria);

                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(
                    It.Is<FederatedCredentialPolicy>(p => p.Criteria == complexCriteria),
                    true),
                    Times.Once);

                // Verify CREATE audit record was created
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
            }

            [Fact]
            public async Task WhenValidationThrowsException_ExceptionBubblesAndNoAuditRecord()
            {
                // Arrange
                var user = new User { Key = 10, Username = "testuser" };
                var packageOwner = new User { Key = 20, Username = "testowner" };

                UserService.Setup(x => x.FindByUsername("testowner", false)).Returns(packageOwner);
                CredentialBuilder.Setup(x => x.VerifyScopes(user, It.IsAny<IEnumerable<Scope>>())).Returns(true);

                var exception = new InvalidOperationException("Validation error");
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Throws(exception);

                // Act & Assert
                var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Target.AddPolicyAsync(
                        createdBy: user,
                        packageOwner: "testowner",
                        criteria: """{"test": "value"}""",
                        policyName: "Test Policy",
                        policyType: FederatedCredentialType.EntraIdServicePrincipal));

                Assert.Same(exception, actualException);

                // Verify policy was not saved
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(It.IsAny<FederatedCredentialPolicy>(), It.IsAny<bool>()), Times.Never);

                // Verify NO audit record was created (exception bubbled up before audit logging)
                AssertNoAudits();
            }
        }

        public class TheUpdatePolicyAsyncMethod : FederatedCredentialServiceFacts
        {
            private const string NewPolicyName = "Updated Policy";
            private const string NewPolicyCriteria = "{\"repository\":\"owner/updated-repo\"}";
            public FederatedCredentialPolicy PolicyToUpdate;

            public TheUpdatePolicyAsyncMethod()
            {
                PolicyToUpdate = Policies[0];
                
                // Set up default mock behavior for Evaluator.ValidatePolicy to return Success
                // This is needed for UpdatePolicyAsync tests since they call ValidatePolicyAsync
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(FederatedCredentialPolicyValidationResult.Success(PolicyToUpdate));
            }

            [Fact]
            public async Task UpdatesPolicyNameAndCriteria()
            {
                // Act
                await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName);

                // Assert
                Assert.Equal(NewPolicyName, PolicyToUpdate.PolicyName);
                Assert.Equal(NewPolicyCriteria, PolicyToUpdate.Criteria);

                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                AssertUpdateAudit();
            }

            [Fact]
            public async Task UpdatesOnlyPolicyName()
            {
                // Arrange
                var policyCriteria = PolicyToUpdate.Criteria;

                // Act
                await Target.UpdatePolicyAsync(PolicyToUpdate, PolicyToUpdate.Criteria, NewPolicyName);

                // Assert
                Assert.Equal(NewPolicyName, PolicyToUpdate.PolicyName);
                Assert.Equal(policyCriteria, PolicyToUpdate.Criteria);

                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                AssertUpdateAudit();
            }

            [Fact]
            public async Task UpdatesOnlyCriteria()
            {
                // Arrange
                var policyName = PolicyToUpdate.PolicyName;

                // Act
                await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, PolicyToUpdate.PolicyName);

                // Assert
                Assert.Equal(policyName, PolicyToUpdate.PolicyName);
                Assert.Equal(NewPolicyCriteria, PolicyToUpdate.Criteria);

                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                AssertUpdateAudit();
            }

            [Fact]
            public async Task UpdatesNothingWhenValuesUnchanged()
            {
                // Arrange
                var policyName = PolicyToUpdate.PolicyName;
                var policyCriteria = PolicyToUpdate.Criteria;

                // Act
                var result = await Target.UpdatePolicyAsync(PolicyToUpdate, PolicyToUpdate.Criteria, PolicyToUpdate.PolicyName);

                // Assert
                Assert.Equal(policyName, PolicyToUpdate.PolicyName);
                Assert.Equal(policyCriteria, PolicyToUpdate.Criteria);
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);

                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);
                AssertNoAudits();
            }

            [Fact]
            public async Task WhenPermissionValidationFails_ReturnsUnauthorizedAndCreatesAuditRecord()
            {
                // Arrange
                CredentialBuilder.Setup(x => x.VerifyScopes(PolicyToUpdate.CreatedBy, It.IsAny<IEnumerable<Scope>>())).Returns(false);

                // Act
                var result = await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Unauthorized, result.Type);
                Assert.Contains("does not have the required permissions", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.PackageOwner), result.PolicyPropertyName);

                // Verify policy was not saved
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);

                // Verify audit record was created for unauthorized access
                AssertUnauthorized(PolicyToUpdate.CreatedBy, PolicyToUpdate.PackageOwner, PolicyToUpdate.Type, NewPolicyCriteria, result.UserMessage!);
            }

            [Fact]
            public async Task WhenEvaluatorValidationFails_ReturnsValidationResultAndCreatesAuditRecord()
            {
                // Arrange
                var validationResult = FederatedCredentialPolicyValidationResult.BadRequest(
                    "Invalid criteria format",
                    nameof(FederatedCredentialPolicy.Criteria));

                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Returns(validationResult);

                // Act
                var result = await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName);

                // Assert
                Assert.Same(validationResult, result);
                Assert.Equal(FederatedCredentialPolicyValidationResultType.BadRequest, result.Type);
                Assert.Equal("Invalid criteria format", result.UserMessage);
                Assert.Equal(nameof(FederatedCredentialPolicy.Criteria), result.PolicyPropertyName);

                // Verify policy was not saved
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);

                // Verify audit record was created for validation failure
                AssertBadRequest(PolicyToUpdate.CreatedBy, PolicyToUpdate.PackageOwner, PolicyToUpdate.Type, NewPolicyCriteria, "Invalid criteria format");
            }

            [Fact]
            public async Task WhenValidationThrowsException_ExceptionBubblesAndNoAuditRecord()
            {
                // Arrange
                var exception = new InvalidOperationException("Validation error");
                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Throws(exception);

                // Act & Assert
                var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName));

                Assert.Same(exception, actualException);

                // Verify policy was not saved
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);

                // Verify NO audit record was created (exception bubbled up before audit logging)
                AssertNoAudits();
            }

            [Fact]
            public async Task WhenPolicyHasMultipleCredentials_RemovesAllCredentials()
            {
                // Arrange
                var credential1 = new Credential { Key = 1, Type = CredentialTypes.ApiKey.V4 };
                var credential2 = new Credential { Key = 2, Type = CredentialTypes.ApiKey.V4 };
                var credential3 = new Credential { Key = 3, Type = CredentialTypes.ApiKey.V4 };

                FederatedCredentialRepository.Setup(x => x.GetShortLivedApiKeysForPolicy(PolicyToUpdate.Key))
                    .Returns(new List<Credential> { credential1, credential2, credential3 });

                // Act
                await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName);

                // Assert
                AuthenticationService.Verify(x => x.RemoveCredential(PolicyToUpdate.CreatedBy, credential1, false), Times.Once);
                AuthenticationService.Verify(x => x.RemoveCredential(PolicyToUpdate.CreatedBy, credential2, false), Times.Once);
                AuthenticationService.Verify(x => x.RemoveCredential(PolicyToUpdate.CreatedBy, credential3, false), Times.Once);

                AssertUpdateAudit();
            }

            [Fact]
            public async Task WhenPolicyHasNoCredentials_UpdatesSuccessfully()
            {
                // Arrange
                FederatedCredentialRepository.Setup(x => x.GetShortLivedApiKeysForPolicy(PolicyToUpdate.Key))
                    .Returns(new List<Credential>());

                // Act
                var result = await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Equal(NewPolicyName, PolicyToUpdate.PolicyName);
                Assert.Equal(NewPolicyCriteria, PolicyToUpdate.Criteria);

                AuthenticationService.Verify(x => x.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>(), It.IsAny<bool>()), Times.Never);
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);

                AssertUpdateAudit();
            }

            [Fact]
            public async Task WhenValidationChangesFields_UpdatesWithValidatedValues()
            {
                // Arrange
                var modifiedCriteria = """{"modified":"by-validator"}""";
                var modifiedPolicy = new FederatedCredentialPolicy
                {
                    Key = PolicyToUpdate.Key,
                    CreatedBy = PolicyToUpdate.CreatedBy,
                    PackageOwner = PolicyToUpdate.PackageOwner,
                    PolicyName = NewPolicyName,
                    Criteria = modifiedCriteria, // Validator modified the criteria
                    Type = PolicyToUpdate.Type,
                };

                Evaluator.Setup(x => x.ValidatePolicy(It.IsAny<FederatedCredentialPolicy>()))
                    .Callback<FederatedCredentialPolicy>(p => p.Criteria = modifiedCriteria)
                    .Returns(FederatedCredentialPolicyValidationResult.Success(modifiedPolicy));

                // Act
                var result = await Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName);

                // Assert
                Assert.Equal(FederatedCredentialPolicyValidationResultType.Success, result.Type);
                Assert.Equal(NewPolicyName, PolicyToUpdate.PolicyName);
                Assert.Equal(modifiedCriteria, PolicyToUpdate.Criteria); // Should use validator-modified criteria

                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Once);
                AssertUpdateAudit();
            }

            [Fact]
            public async Task WhenCredentialRemovalFails_ExceptionBubblesUp()
            {
                // Arrange
                var credential = new Credential { Key = 1, Type = CredentialTypes.ApiKey.V4 };
                FederatedCredentialRepository.Setup(x => x.GetShortLivedApiKeysForPolicy(PolicyToUpdate.Key))
                    .Returns(new List<Credential> { credential });

                var exception = new InvalidOperationException("Failed to remove credential");
                AuthenticationService.Setup(x => x.RemoveCredential(PolicyToUpdate.CreatedBy, credential, false))
                    .ThrowsAsync(exception);

                // Act & Assert
                var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName));

                Assert.Same(exception, actualException);

                // Verify policy was not saved
                FederatedCredentialRepository.Verify(x => x.SavePoliciesAsync(), Times.Never);
                AssertNoAudits();
            }

            [Fact]
            public async Task WhenSavePoliciesFails_ExceptionBubblesUp()
            {
                // Arrange
                var exception = new InvalidOperationException("Database save failed");
                FederatedCredentialRepository.Setup(x => x.SavePoliciesAsync())
                    .ThrowsAsync(exception);

                // Act & Assert
                var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Target.UpdatePolicyAsync(PolicyToUpdate, NewPolicyCriteria, NewPolicyName));

                Assert.Same(exception, actualException);

                // Verify audit record was not created
                AssertNoAudits();
            }

            private void AssertUpdateAudit()
            {
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Update, policyAudit.Action);
            }
        }

        public class TheDeletePolicyAsyncMethod : FederatedCredentialServiceFacts
        {
            [Fact]
            public async Task DeletesCredentialAndPolicies()
            {
                // Act
                await Target.DeletePolicyAsync(Policies[0]);

                // Assert
                AuthenticationService.Verify(x => x.RemoveCredential(Policies[0].CreatedBy, Credential, false), Times.Once);
                FederatedCredentialRepository.Verify(x => x.DeletePolicyAsync(Policies[0], true), Times.Once);

                AssertDeleteAudit();
            }

            private void AssertDeleteAudit()
            {
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Delete, policyAudit.Action);
            }
        }

        public class TheGenerateApiKeyAsyncMethod : FederatedCredentialServiceFacts
        {
            [Fact]
            public async Task NoMatchingPolicyForNonExistentUser()
            {
                // Act
                var result = await Target.GenerateApiKeyAsync("someone else", BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("No matching trust policy owned by user 'someone else' was found.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task NoMatchingPolicyWhenEvaluatorFindsNoMatch()
            {
                // Arrange
                Evaluator
                    .Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders))
                    .ReturnsAsync(() => OidcTokenEvaluationResult.NoMatchingPolicy());

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("No matching trust policy owned by user 'jim' was found.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task UnauthorizedWhenEvaluatorReturnsBadToken()
            {
                // Arrange
                Evaluator
                    .Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders))
                    .ReturnsAsync(() => OidcTokenEvaluationResult.BadToken("That token is missing a thing or two."));

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("That token is missing a thing or two.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsOrganizationCurrentUser()
            {
                // Arrange
                CurrentUser = new Organization { Key = CurrentUser.Key, Username = CurrentUser.Username };

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.StartsWith("Generating fetching tokens directly for organizations is not supported.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsDeletedUser()
            {
                // Arrange
                CurrentUser.IsDeleted = true;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The user 'jim' is deleted.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsLockedUser()
            {
                // Arrange
                CurrentUser.UserStatusKey = UserStatus.Locked;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The user 'jim' is locked.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsUnconfirmedUser()
            {
                // Arrange
                CurrentUser.EmailAddress = null;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The user 'jim' does not have a confirmed email address.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsMissingPackageOwner()
            {
                // Arrange
                UserService.Setup(x => x.FindByKey(PackageOwner.Key, false)).Returns(() => null!);

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The package owner of the match trust policy not longer exists.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsDeletedPackageOwner()
            {
                // Arrange
                PackageOwner.IsDeleted = true;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The organization 'jim-org' is deleted.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsLockedPackageOwner()
            {
                // Arrange
                PackageOwner.UserStatusKey = UserStatus.Locked;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The organization 'jim-org' is locked.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsUnconfirmedPackageOwner()
            {
                // Arrange
                PackageOwner.UserStatusKey = UserStatus.Locked;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The organization 'jim-org' is locked.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsCredentialWithInvalidScopes()
            {
                // Arrange
                CredentialBuilder.Setup(x => x.VerifyScopes(CurrentUser, Credential.Scopes)).Returns(false);

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.StartsWith("The scopes on the generated API key are not valid.", result.UserMessage);
                CredentialBuilder.Verify(x => x.VerifyScopes(CurrentUser, Credential.Scopes), Times.Once);

                Assert.Null(Evaluation.MatchedPolicy.LastMatched);

                AssertNoAudits();
            }

            /// <summary>
            /// See <see cref="NuGet.Services.Entities.ExceptionExtensions.IsSqlUniqueConstraintViolation(System.Data.DataException)"/>
            /// for error codes.
            /// </summary>
            [Theory]
            [InlineData(547)]
            [InlineData(2601)]
            [InlineData(2627)]
            public async Task RejectsSaveViolatingUniqueConstraint(int sqlErrorCode)
            {
                // Arrange
                var sqlException = GetSqlException(sqlErrorCode);
                AuthenticationService
                    .Setup(x => x.AddCredential(CurrentUser, Credential))
                    .ThrowsAsync(new DbUpdateException("Fail!", sqlException));

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("This bearer token has already been used. A new bearer token must be used for each request.", result.UserMessage);
                FederatedCredentialRepository.Verify(x => x.SaveFederatedCredentialAsync(Evaluation.FederatedCredential, false), Times.Once);

                Assert.Equal(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc), Evaluation.MatchedPolicy.LastMatched);

                AssertRejectReplayAudit();
            }

            [Fact]
            public async Task DoesNotHandleOtherSqlExceptions()
            {
                // Arrange
                var exception = new DbUpdateException("Fail!", GetSqlException(123));
                AuthenticationService
                    .Setup(x => x.AddCredential(CurrentUser, Credential))
                    .ThrowsAsync(exception);

                // Act
                var actual = await Assert.ThrowsAsync<DbUpdateException>(() => Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders));
                Assert.Same(actual, exception);

                AssertNoAudits();
            }

            [Fact]
            public async Task ReturnsCreatedApiKey()
            {
                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Created, result.Type);
                Assert.Equal("secret", result.PlaintextApiKey);
                Assert.Equal(new DateTimeOffset(2024, 10, 11, 9, 30, 0, TimeSpan.Zero), result.Expires);

                Assert.Same(PackageOwner, Evaluation.MatchedPolicy.PackageOwner);
                Assert.Equal(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc), Evaluation.MatchedPolicy.LastMatched);

                UserService.Verify(x => x.FindByUsername(CurrentUser.Username, false), Times.Once);
                FederatedCredentialRepository.Verify(x => x.GetPoliciesCreatedByUser(CurrentUser.Key), Times.Once);
                Evaluator.Verify(x => x.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders), Times.Once);
                UserService.Verify(x => x.FindByKey(PackageOwner.Key, false), Times.Once);
                CredentialBuilder.Verify(x => x.CreateShortLivedApiKey(TimeSpan.FromMinutes(15), Evaluation.MatchedPolicy, It.IsAny<string>(), out PlaintextApiKey), Times.Once);
                CredentialBuilder.Verify(x => x.VerifyScopes(CurrentUser, Credential.Scopes), Times.Once);
                FederatedCredentialRepository.Verify(x => x.SaveFederatedCredentialAsync(Evaluation.FederatedCredential, false), Times.Once);
                AuthenticationService.Verify(x => x.AddCredential(CurrentUser, Credential), Times.Once);

                AssertExchangeForApiKeyAudit();
            }

            private void AssertExchangeForApiKeyAudit()
            {
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.ExchangeForApiKey, policyAudit.Action);
            }
        }

        public FederatedCredentialServiceFacts()
        {
            UserService = new Mock<IUserService>();
            FederatedCredentialRepository = new Mock<IFederatedCredentialRepository>();
            Evaluator = new Mock<IFederatedCredentialPolicyEvaluator>();
            CredentialBuilder = new Mock<ICredentialBuilder>();
            AuthenticationService = new Mock<IAuthenticationService>();
            AuditingService = new Mock<IAuditingService>();
            DateTimeProvider = new Mock<IDateTimeProvider>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            GalleryConfigurationService = new Mock<IGalleryConfigurationService>();

            BearerToken = "my-token";
            CurrentUser = new User { Key = 1, Username = "jim", EmailAddress = "jim@localhost" };
            PackageOwner = new Organization { Key = 2, Username = "jim-org", EmailAddress = "jim-org@localhost" };
            Policies = new List<FederatedCredentialPolicy>
            {
                new() { Key = 3, CreatedBy = CurrentUser, CreatedByUserKey = CurrentUser.Key, PackageOwner = PackageOwner, PackageOwnerUserKey = PackageOwner.Key, Criteria = "{}" }
            };
            Evaluation = OidcTokenEvaluationResult.NewMatchedPolicy(
                matchedPolicy: Policies[0],
                federatedCredential: new FederatedCredential());
            PlaintextApiKey = null;
            Credential = new Credential { Scopes = [], Expires = new DateTime(2024, 10, 11, 9, 30, 0, DateTimeKind.Utc) };
            RequestHeaders = new NameValueCollection();

            EntraIdServicePrincipalCriteria = """{"tid":"58fa0116-d469-4fc9-83c8-9b1a8706d9cc","oid":"4ab4b916-b6de-4412-aee0-808ef692b270"}""";

            UserService.Setup(x => x.FindByUsername(CurrentUser.Username, false)).Returns(() => CurrentUser);
            UserService.Setup(x => x.FindByKey(PackageOwner.Key, false)).Returns(() => PackageOwner);
            FederatedCredentialRepository.Setup(x => x.GetPoliciesCreatedByUser(CurrentUser.Key)).Returns(() => Policies);
            FederatedCredentialRepository.Setup(x => x.GetShortLivedApiKeysForPolicy(Policies[0].Key)).Returns(() => [Credential]);
            FederatedCredentialRepository.Setup(x => x.SavePoliciesAsync()).Returns(Task.CompletedTask);
            Evaluator.Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders)).ReturnsAsync(() => Evaluation);
            CredentialBuilder
                .Setup(x => x.CreateShortLivedApiKey(TimeSpan.FromMinutes(15), Evaluation.MatchedPolicy, It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(new CreateShortLivedApiKey((TimeSpan expires, FederatedCredentialPolicy policy, string galleryEnvironment, out string plaintextApiKey) =>
                {
                    plaintextApiKey = "secret";
                    return Credential;
                }));
            CredentialBuilder.Setup(x => x.VerifyScopes(CurrentUser, It.IsAny<IEnumerable<Scope>>())).Returns(true);
            Configuration.Setup(x => x.ShortLivedApiKeyDuration).Returns(TimeSpan.FromMinutes(15));
            GalleryConfigurationService.Setup(x => x.Current.Environment).Returns("TestEnv");
            DateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc));

            Target = new FederatedCredentialService(
                UserService.Object,
                FederatedCredentialRepository.Object,
                Evaluator.Object,
                CredentialBuilder.Object,
                AuthenticationService.Object,
                AuditingService.Object,
                DateTimeProvider.Object,
                Configuration.Object,
                GalleryConfigurationService.Object);
        }

        delegate Credential CreateShortLivedApiKey(TimeSpan expires, FederatedCredentialPolicy policy, string galleryEnvironment, out string plaintextApiKey);

        public Mock<IUserService> UserService { get; }
        public Mock<IFederatedCredentialRepository> FederatedCredentialRepository { get; }
        public Mock<IFederatedCredentialPolicyEvaluator> Evaluator { get; }
        public Mock<ICredentialBuilder> CredentialBuilder { get; }
        public Mock<IAuthenticationService> AuthenticationService { get; }
        public Mock<IAuditingService> AuditingService { get; }
        public Mock<IDateTimeProvider> DateTimeProvider { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public Mock<IGalleryConfigurationService> GalleryConfigurationService { get; }
        public string BearerToken { get; }
        public User CurrentUser { get; set; }
        public User PackageOwner { get; }
        public List<FederatedCredentialPolicy> Policies { get; }
        public OidcTokenEvaluationResult Evaluation { get; }
        public string? PlaintextApiKey;
        public Credential Credential { get; }
        public NameValueCollection RequestHeaders { get; }
        public string EntraIdServicePrincipalCriteria { get; }
        public FederatedCredentialService Target { get; }

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

        protected void AssertNoAudits()
        {
            AuditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
        }

        protected void AssertBadRequest(User createdBy, User packageOwner, FederatedCredentialType policyType, string criteria, string failureReason)
        {
            var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
            var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
            Assert.Equal(AuditedFederatedCredentialPolicyAction.BadRequest, policyAudit.Action);

            // Verify the audit record was created with the expected parameters
            AuditingService.Verify(x => x.SaveAuditRecordAsync(
                It.Is<FederatedCredentialPolicyAuditRecord>(audit =>
                    audit.Action == AuditedFederatedCredentialPolicyAction.BadRequest &&
                    audit.Type == policyType.ToString() &&
                    audit.Criteria == criteria &&
                    audit.ErrorMessage == failureReason
                )), Times.Once);
        }

        protected void AssertUnauthorized(User createdBy, User packageOwner, FederatedCredentialType policyType, string criteria, string failureReason)
        {
            var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
            var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
            Assert.Equal(AuditedFederatedCredentialPolicyAction.Unauthorized, policyAudit.Action);

            // Verify the audit record was created with the expected parameters
            AuditingService.Verify(x => x.SaveAuditRecordAsync(
                It.Is<FederatedCredentialPolicyAuditRecord>(audit =>
                    audit.Action == AuditedFederatedCredentialPolicyAction.Unauthorized &&
                    audit.Type == policyType.ToString() &&
                    audit.Criteria == criteria &&
                    audit.ErrorMessage == failureReason
                )), Times.Once);
        }

        private void AssertRejectReplayAudit()
        {
            var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
            var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
            Assert.Equal(AuditedFederatedCredentialPolicyAction.RejectReplay, policyAudit.Action);
        }

        public static SqlException GetSqlException(int sqlErrorCode)
        {
            var sqlError = Activator.CreateInstance(
                typeof(SqlError),
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                args: [sqlErrorCode, (byte)2, (byte)3, "server", "error", "procedure", 4],
                culture: null);
            var sqlErrorCollection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true);
            typeof(SqlErrorCollection)
                .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(sqlErrorCollection, [sqlError]);
            var sqlException = (SqlException)typeof(SqlException)
                .GetMethod(
                    "CreateException",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(SqlErrorCollection), typeof(string)],
                    modifiers: null)
                .Invoke(null, [sqlErrorCollection, "16.0"]);
            return sqlException;
        }
    }
}
