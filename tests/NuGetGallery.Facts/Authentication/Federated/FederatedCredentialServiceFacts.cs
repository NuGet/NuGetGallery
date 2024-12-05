// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class FederatedCredentialServiceFacts
    {
        public class TheGenerateApiKeyAsyncMethod : FederatedCredentialServiceFacts
        {
            [Fact]
            public async Task NoMatchingPolicyForNonExistentUser()
            {
                // Act
                var result = await Target.GenerateApiKeyAsync("someone else", BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("No matching federated credential trust policy owned by user 'someone else' was found.", result.UserMessage);
            }

            [Fact]
            public async Task NoMatchingPolicyWhenEvaluatorFindsNoMatch()
            {
                // Arrange
                FederatedCredentialEvaluator
                    .Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken))
                    .ReturnsAsync(() => EvaluatedFederatedCredentialPolicies.NoMatchingPolicy([]));

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("No matching federated credential trust policy owned by user 'jim' was found.", result.UserMessage);
            }

            [Fact]
            public async Task UnauthorizedWhenEvaluatorReturnsBadToken()
            {
                // Arrange
                FederatedCredentialEvaluator
                    .Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken))
                    .ReturnsAsync(() => EvaluatedFederatedCredentialPolicies.BadToken("That token is missing a thing or two."));

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("That token is missing a thing or two.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsOrganizationCurrentUser()
            {
                // Arrange
                CurrentUser = new Organization { Key = CurrentUser.Key, Username = CurrentUser.Username };

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.StartsWith("Generating fetching tokens directly for organizations is not supported.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsDeletedUser()
            {
                // Arrange
                CurrentUser.IsDeleted = true;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The user 'jim' is deleted.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsLockedUser()
            {
                // Arrange
                CurrentUser.UserStatusKey = UserStatus.Locked;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The user 'jim' is locked.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsUnconfirmedUser()
            {
                // Arrange
                CurrentUser.EmailAddress = null;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The user 'jim' does not have a confirmed email address.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsMissingPackageOwner()
            {
                // Arrange
                UserService.Setup(x => x.FindByKey(PackageOwner.Key, false)).Returns(() => null!);

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The package owner of the match federated credential trust policy not longer exists.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsDeletedPackageOwner()
            {
                // Arrange
                PackageOwner.IsDeleted = true;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The organization 'jim-org' is deleted.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsLockedPackageOwner()
            {
                // Arrange
                PackageOwner.UserStatusKey = UserStatus.Locked;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The organization 'jim-org' is locked.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsUnconfirmedPackageOwner()
            {
                // Arrange
                PackageOwner.UserStatusKey = UserStatus.Locked;

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The organization 'jim-org' is locked.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsPackageOwnerNotInFlight()
            {
                // Arrange
                FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(false);

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.Equal("The package owner 'jim-org' is not enabled to use federated credentials.", result.UserMessage);
            }

            [Fact]
            public async Task RejectsCredentialWithInvalidScopes()
            {
                // Arrange
                CredentialBuilder.Setup(x => x.VerifyScopes(CurrentUser, Credential.Scopes)).Returns(false);

                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.BadRequest, result.Type);
                Assert.StartsWith("The scopes on the generated API key are not valid.", result.UserMessage);
                CredentialBuilder.Verify(x => x.VerifyScopes(CurrentUser, Credential.Scopes), Times.Once);

                Assert.Null(Evaluation.MatchedPolicy.LastMatched);
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
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Unauthorized, result.Type);
                Assert.Equal("This bearer token has already been used. A new bearer token must be used for each request.", result.UserMessage);
                FederatedCredentialRepository.Verify(x => x.SaveFederatedCredentialAsync(Evaluation.FederatedCredential, false), Times.Once);

                Assert.Equal(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc), Evaluation.MatchedPolicy.LastMatched);
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
                var actual = await Assert.ThrowsAsync<DbUpdateException>(() => Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken));
                Assert.Same(actual, exception);
            }

            [Fact]
            public async Task ReturnsCreatedApiKey()
            {
                // Act
                var result = await Target.GenerateApiKeyAsync(CurrentUser.Username, BearerToken);

                // Assert
                Assert.Equal(GenerateApiKeyResultType.Created, result.Type);
                Assert.Equal("secret", result.PlaintextApiKey);
                Assert.Equal(new DateTimeOffset(2024, 10, 11, 9, 30, 0, TimeSpan.Zero), result.Expires);

                Assert.Same(PackageOwner, Evaluation.MatchedPolicy.PackageOwner);
                Assert.Equal(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc), Evaluation.MatchedPolicy.LastMatched);

                UserService.Verify(x => x.FindByUsername(CurrentUser.Username, false), Times.Once);
                FederatedCredentialRepository.Verify(x => x.GetPoliciesCreatedByUser(CurrentUser.Key), Times.Once);
                FederatedCredentialEvaluator.Verify(x => x.GetMatchingPolicyAsync(Policies, BearerToken), Times.Once);
                UserService.Verify(x => x.FindByKey(PackageOwner.Key, false), Times.Once);
                CredentialBuilder.Verify(x => x.CreateShortLivedApiKey(TimeSpan.FromMinutes(15), Evaluation.MatchedPolicy, out PlaintextApiKey), Times.Once);
                CredentialBuilder.Verify(x => x.VerifyScopes(CurrentUser, Credential.Scopes), Times.Once);
                FederatedCredentialRepository.Verify(x => x.SaveFederatedCredentialAsync(Evaluation.FederatedCredential, false), Times.Once);
                AuthenticationService.Verify(x => x.AddCredential(CurrentUser, Credential), Times.Once);
            }
        }

        public FederatedCredentialServiceFacts()
        {
            UserService = new Mock<IUserService>();
            FederatedCredentialRepository = new Mock<IFederatedCredentialRepository>();
            FederatedCredentialEvaluator = new Mock<IFederatedCredentialEvaluator>();
            CredentialBuilder = new Mock<ICredentialBuilder>();
            AuthenticationService = new Mock<IAuthenticationService>();
            FeatureFlagService = new Mock<IFeatureFlagService>();
            DateTimeProvider = new Mock<IDateTimeProvider>();
            Configuration = new Mock<IFederatedCredentialConfiguration>();

            BearerToken = "my-token";
            CurrentUser = new User { Key = 1, Username = "jim", EmailAddress = "jim@localhost" };
            PackageOwner = new Organization { Key = 2, Username = "jim-org", EmailAddress = "jim-org@localhost" };
            Policies = new List<FederatedCredentialPolicy>();
            Evaluation = EvaluatedFederatedCredentialPolicies.NewMatchedPolicy(
                results: [],
                matchedPolicy: new FederatedCredentialPolicy { PackageOwnerUserKey = PackageOwner.Key },
                federatedCredential: new FederatedCredential());
            PlaintextApiKey = null;
            Credential = new Credential { Scopes = [], Expires = new DateTime(2024, 10, 11, 9, 30, 0, DateTimeKind.Utc) };

            UserService.Setup(x => x.FindByUsername(CurrentUser.Username, false)).Returns(() => CurrentUser);
            UserService.Setup(x => x.FindByKey(PackageOwner.Key, false)).Returns(() => PackageOwner);
            FederatedCredentialRepository.Setup(x => x.GetPoliciesCreatedByUser(CurrentUser.Key)).Returns(() => Policies);
            FederatedCredentialEvaluator.Setup(x => x.GetMatchingPolicyAsync(Policies, BearerToken)).ReturnsAsync(() => Evaluation);
            FeatureFlagService.Setup(x => x.CanUseFederatedCredentials(PackageOwner)).Returns(true);
            CredentialBuilder
                .Setup(x => x.CreateShortLivedApiKey(TimeSpan.FromMinutes(15), Evaluation.MatchedPolicy, out It.Ref<string>.IsAny))
                .Returns(new CreateShortLivedApiKey((TimeSpan expires, FederatedCredentialPolicy policy, out string plaintextApiKey) =>
                {
                    plaintextApiKey = "secret";
                    return Credential;
                }));
            CredentialBuilder.Setup(x => x.VerifyScopes(CurrentUser, Credential.Scopes)).Returns(true);
            Configuration.Setup(x => x.ShortLivedApiKeyDuration).Returns(TimeSpan.FromMinutes(15));
            DateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2024, 10, 12, 12, 30, 0, DateTimeKind.Utc));

            Target = new FederatedCredentialService(
                UserService.Object,
                FederatedCredentialRepository.Object,
                FederatedCredentialEvaluator.Object,
                CredentialBuilder.Object,
                AuthenticationService.Object,
                DateTimeProvider.Object,
                FeatureFlagService.Object,
                Configuration.Object);
        }

        delegate Credential CreateShortLivedApiKey(TimeSpan expires, FederatedCredentialPolicy policy, out string plaintextApiKey);

        public Mock<IUserService> UserService { get; }
        public Mock<IFederatedCredentialRepository> FederatedCredentialRepository { get; }
        public Mock<IFederatedCredentialEvaluator> FederatedCredentialEvaluator { get; }
        public Mock<ICredentialBuilder> CredentialBuilder { get; }
        public Mock<IAuthenticationService> AuthenticationService { get; }
        public Mock<IFeatureFlagService> FeatureFlagService { get; }
        public Mock<IDateTimeProvider> DateTimeProvider { get; }
        public Mock<IFederatedCredentialConfiguration> Configuration { get; }
        public string BearerToken { get; }
        public User CurrentUser { get; set; }
        public User PackageOwner { get; }
        public List<FederatedCredentialPolicy> Policies { get; }
        public EvaluatedFederatedCredentialPolicies Evaluation { get; }
        public string? PlaintextApiKey;
        public Credential Credential { get; }
        public FederatedCredentialService Target { get; }

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
