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
        public class TheAddEntraIdServicePrincipalPolicyAsyncMethod : FederatedCredentialServiceFacts
        {
            [Fact]
            public async Task RejectsOrganizationPolicyUser()
            {
                // Arrange
                CurrentUser = new Organization { Key = CurrentUser.Key, Username = CurrentUser.Username };

                // Act
                var result = await Target.AddEntraIdServicePrincipalPolicyAsync(CurrentUser, PackageOwner, EntraIdServicePrincipalCriteria);

                // Assert
                Assert.Equal(AddFederatedCredentialPolicyResultType.BadRequest, result.Type);
                Assert.StartsWith($"Policy user '{CurrentUser.Username}' is an organization.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsFailedScopes()
            {
                // Arrange
                CredentialBuilder.Setup(x => x.VerifyScopes(CurrentUser, It.IsAny<IEnumerable<Scope>>())).Returns(false);

                // Act
                var result = await Target.AddEntraIdServicePrincipalPolicyAsync(CurrentUser, PackageOwner, EntraIdServicePrincipalCriteria);

                // Assert
                Assert.Equal(AddFederatedCredentialPolicyResultType.BadRequest, result.Type);
                Assert.StartsWith($"The user '{CurrentUser.Username}' does not have the required permissions", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsPackageOwnerNotInFlight()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(false);

                // Act
                var result = await Target.AddEntraIdServicePrincipalPolicyAsync(CurrentUser, PackageOwner, EntraIdServicePrincipalCriteria);

                // Assert
                Assert.Equal(AddFederatedCredentialPolicyResultType.BadRequest, result.Type);
                Assert.StartsWith($"The package owner '{PackageOwner.Username}' is not enabled to use federated credentials.", result.UserMessage);

                AssertNoAudits();
            }

            [Fact]
            public async Task RejectsTenantIdNotInAllowList()
            {
                // Arrange
                EntraIdTokenValidator.Setup(x => x.IsTenantAllowed(It.IsAny<Guid>())).Returns(false);

                // Act
                var result = await Target.AddEntraIdServicePrincipalPolicyAsync(CurrentUser, PackageOwner, EntraIdServicePrincipalCriteria);

                // Assert
                Assert.Equal(AddFederatedCredentialPolicyResultType.BadRequest, result.Type);
                Assert.StartsWith($"The Entra ID tenant '{EntraIdServicePrincipalCriteria.TenantId}' is not in the allow list.", result.UserMessage);

                Assert.Empty(FederatedCredentialRepository.Invocations);

                AssertNoAudits();
            }

            [Fact]
            public async Task AddsPolicy()
            {
                // Act
                var result = await Target.AddEntraIdServicePrincipalPolicyAsync(CurrentUser, PackageOwner, EntraIdServicePrincipalCriteria);

                // Assert
                Assert.Equal(AddFederatedCredentialPolicyResultType.Created, result.Type);
                Assert.Equal(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc), result.Policy.Created);
                Assert.Same(CurrentUser, result.Policy.CreatedBy);
                Assert.Same(PackageOwner, result.Policy.PackageOwner);
                Assert.Equal(FederatedCredentialType.EntraIdServicePrincipal, result.Policy.Type);
                Assert.Equal(
                    """
                    {"tid":"58fa0116-d469-4fc9-83c8-9b1a8706d9cc","oid":"4ab4b916-b6de-4412-aee0-808ef692b270"}
                    """, result.Policy.Criteria);
                Assert.Null(result.Policy.LastMatched);

                FeatureFlagService.Verify(x => x.CanUseFederatedCredentials(PackageOwner), Times.Once);
                EntraIdTokenValidator.Verify(x => x.IsTenantAllowed(EntraIdServicePrincipalCriteria.TenantId), Times.Once);
                CredentialBuilder.Verify(x => x.VerifyScopes(CurrentUser, It.IsAny<IEnumerable<Scope>>()), Times.Once);
                FederatedCredentialRepository.Verify(x => x.AddPolicyAsync(result.Policy, true), Times.Once);

                var isTenantAllowed = Assert.Single(CredentialBuilder.Invocations);
                var scopes = Assert.IsAssignableFrom<IEnumerable<Scope>>(isTenantAllowed.Arguments[1]);
                var scope = Assert.Single(scopes);
                Assert.Equal(NuGetScopes.All, scope.AllowedAction);
                Assert.Equal(NuGetPackagePattern.AllInclusivePattern, scope.Subject);
                Assert.Same(PackageOwner, scope.Owner);

                AssertCreateAudit();
            }

            private void AssertCreateAudit()
            {
                var audits = AssertAuditResourceTypes(FederatedCredentialPolicyAuditRecord.ResourceType);
                var policyAudit = Assert.IsType<FederatedCredentialPolicyAuditRecord>(audits[0]);
                Assert.Equal(AuditedFederatedCredentialPolicyAction.Create, policyAudit.Action);
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
            public async Task RejectsPackageOwnerNotInFlight()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(false);

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken, RequestHeaders);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The package owner 'jim-org' is not enabled to use federated credentials.", result.UserMessage);

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
                CredentialBuilder.Verify(x => x.CreateShortLivedApiKey(TimeSpan.FromMinutes(15), Evaluation.MatchedPolicy, It.IsAny<char>(), It.IsAny<bool>(), out PlaintextApiKey), Times.Once);
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
            EntraIdTokenValidator = new Mock<IEntraIdTokenValidator>();
            CredentialBuilder = new Mock<ICredentialBuilder>();
            AuthenticationService = new Mock<IAuthenticationService>();
            AuditingService = new Mock<IAuditingService>();
            FeatureFlagService = new Mock<IFeatureFlagService>();
            DateTimeProvider = new Mock<IDateTimeProvider>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();
            GalleryConfigurationService = new Mock<IGalleryConfigurationService>();

            BearerToken = "my-token";
            CurrentUser = new User { Key = 1, Username = "jim", EmailAddress = "jim@localhost" };
            PackageOwner = new Organization { Key = 2, Username = "jim-org", EmailAddress = "jim-org@localhost" };
            Policies = new List<FederatedCredentialPolicy>
            {
                new() { Key = 3, CreatedBy = CurrentUser, CreatedByUserKey = CurrentUser.Key, PackageOwner = PackageOwner, PackageOwnerUserKey = PackageOwner.Key }
            };
            Evaluation = OidcTokenEvaluationResult.NewMatchedPolicy(
                matchedPolicy: Policies[0],
                federatedCredential: new FederatedCredential());
            PlaintextApiKey = null;
            Credential = new Credential { Scopes = [], Expires = new DateTime(2024, 10, 11, 9, 30, 0, DateTimeKind.Utc) };
            RequestHeaders = new NameValueCollection();

            EntraIdServicePrincipalCriteria = new EntraIdServicePrincipalCriteria(
                tenantId: new Guid("58fa0116-d469-4fc9-83c8-9b1a8706d9cc"),
                objectId: new Guid("4ab4b916-b6de-4412-aee0-808ef692b270"));

            UserService.Setup(x => x.FindByUsername(CurrentUser.Username, false)).Returns(() => CurrentUser);
            UserService.Setup(x => x.FindByKey(PackageOwner.Key, false)).Returns(() => PackageOwner);
            FederatedCredentialRepository.Setup(x => x.GetPoliciesCreatedByUser(CurrentUser.Key)).Returns(() => Policies);
            FederatedCredentialRepository.Setup(x => x.GetShortLivedApiKeysForPolicy(Policies[0].Key)).Returns(() => [Credential]);
            Evaluator.Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken, RequestHeaders)).ReturnsAsync(() => Evaluation);
            FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
            CredentialBuilder
                .Setup(x => x.CreateShortLivedApiKey(TimeSpan.FromMinutes(15), Evaluation.MatchedPolicy, It.IsAny<char>(), It.IsAny<bool>(), out It.Ref<string>.IsAny))
                .Returns(new CreateShortLivedApiKey((TimeSpan expires, FederatedCredentialPolicy policy, char apiKeyEnvironment, bool isApiKeyV5Enabled, out string plaintextApiKey) =>
                {
                    plaintextApiKey = "secret";
                    return Credential;
                }));
            CredentialBuilder.Setup(x => x.VerifyScopes(CurrentUser, It.IsAny<IEnumerable<Scope>>())).Returns(true);
            Configuration.Setup(x => x.ShortLivedApiKeyDuration).Returns(TimeSpan.FromMinutes(15));
            GalleryConfigurationService.Setup(x => x.Current.Environment).Returns("TestEnv");
            DateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc));
            EntraIdTokenValidator.Setup(x => x.IsTenantAllowed(EntraIdServicePrincipalCriteria.TenantId)).Returns(true);

            Target = new FederatedCredentialService(
                UserService.Object,
                FederatedCredentialRepository.Object,
                Evaluator.Object,
                EntraIdTokenValidator.Object,
                CredentialBuilder.Object,
                AuthenticationService.Object,
                AuditingService.Object,
                DateTimeProvider.Object,
                FeatureFlagService.Object,
                Configuration.Object,
                GalleryConfigurationService.Object);
        }

        delegate Credential CreateShortLivedApiKey(TimeSpan expires, FederatedCredentialPolicy policy, char apiKeyEnvironment, bool isApiKeyV5Enabled, out string plaintextApiKey);

        public Mock<IUserService> UserService { get; }
        public Mock<IFederatedCredentialRepository> FederatedCredentialRepository { get; }
        public Mock<IFederatedCredentialPolicyEvaluator> Evaluator { get; }
        public Mock<IEntraIdTokenValidator> EntraIdTokenValidator { get; }
        public Mock<ICredentialBuilder> CredentialBuilder { get; }
        public Mock<IAuthenticationService> AuthenticationService { get; }
        public Mock<IAuditingService> AuditingService { get; }
        public Mock<IFeatureFlagService> FeatureFlagService { get; }
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
        public EntraIdServicePrincipalCriteria EntraIdServicePrincipalCriteria { get; }
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
