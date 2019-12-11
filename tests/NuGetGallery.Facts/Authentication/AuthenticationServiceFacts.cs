// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Owin;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
        public class TheAuthenticatePasswordMethod : TestContainer
        {
            private Fakes _fakes;
            private AuthenticationService _authenticationService;
            private Mock<IDateTimeProvider> _dateTimeProviderMock;

            public TheAuthenticatePasswordMethod()
            {
                _fakes = Get<Fakes>();
                _dateTimeProviderMock = GetMock<IDateTimeProvider>();
                _authenticationService = Get<AuthenticationService>();
            }

            [Fact]
            public async Task GivenNoUserWithName_ItReturnsFailure()
            {
                // Act
                var result = await _authenticationService.Authenticate("notARealUser", "password");

                // Assert
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.BadCredentials, result.Result);
            }

            [Fact]
            public async Task WritesAuditRecordWhenGivenNoUserWithName()
            {
                // Act
                await _authenticationService.Authenticate("notARealUser", "password");

                // Assert
                Assert.True(_authenticationService.Auditing.WroteRecord<FailedAuthenticatedOperationAuditRecord>(ar =>
                    ar.Action == AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser &&
                    ar.UsernameOrEmail == "notARealUser"));
            }

            [Fact]
            public async Task GivenUserNameDoesNotMatchPassword_ItReturnsFailure()
            {
                // Act
                var result = await _authenticationService.Authenticate(_fakes.User.Username, "bogus password!!");

                // Assert
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.BadCredentials, result.Result);
            }

            [Fact]
            public async Task GivenAnOrganization_ItReturnsFailure()
            {
                // Act
                var result = await _authenticationService.Authenticate(_fakes.Organization.Username, Fakes.Password);

                // Assert
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.BadCredentials, result.Result);
            }

            [Fact]
            public async Task WritesAuditRecordWhenGivenUserNameDoesNotMatchPassword()
            {
                // Act
                await _authenticationService.Authenticate(_fakes.User.Username, "bogus password!!");

                // Assert
                Assert.True(_authenticationService.Auditing.WroteRecord<FailedAuthenticatedOperationAuditRecord>(ar =>
                    ar.Action == AuditedAuthenticatedOperationAction.FailedLoginInvalidPassword &&
                    ar.UsernameOrEmail == _fakes.User.Username));
            }

            [Fact]
            public async Task GivenUserNameWithMatchingPasswordCredential_ItReturnsAuthenticatedUser()
            {
                // Arrange
                var user = _fakes.User;

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                var expectedCred = user.Credentials.SingleOrDefault(
                    c => string.Equals(c.Type, CredentialBuilder.LatestPasswordType, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.Success, result.Result);
                Assert.Same(user, result.AuthenticatedUser.User);
                Assert.Same(expectedCred, result.AuthenticatedUser.CredentialUsed);
            }


            public static IEnumerable<object[]>
                GivenUserNameWithMatchingOldPasswordCredential_ItMigratesHashToLatest_Input
            {
                get
                {
                    return new[]
                    {
                        new object[] {new Func<Fakes, User>(f => f.Pbkdf2User)},
                        new object[] {new Func<Fakes, User>(f => f.ShaUser)}
                    };
                }
            }

            [Theory, MemberData("GivenUserNameWithMatchingOldPasswordCredential_ItMigratesHashToLatest_Input")]
            public async Task GivenUserNameWithMatchingOldPasswordCredential_ItMigratesHashToLatest(
                Func<Fakes, User> getUser)
            {
                // Arrange
                var user = getUser(_fakes);

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                var expectedCred = user.Credentials.SingleOrDefault(
                    c => string.Equals(c.Type, CredentialBuilder.LatestPasswordType, StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(expectedCred);
                Assert.True(VerifyPasswordHash(expectedCred.Value, CredentialBuilder.LatestPasswordType, Fakes.Password));
            }

            public static IEnumerable<object[]>
                GivenUserNameWithMatchingOldPasswordCredential_ItWritesAuditRecordsOfMigration_Input
            {
                get
                {
                    return new[]
                    {
                        new object[] {new Func<Fakes, User>(f => f.Pbkdf2User)},
                        new object[] {new Func<Fakes, User>(f => f.ShaUser)}
                    };
                }
            }

            [Theory, MemberData("GivenUserNameWithMatchingOldPasswordCredential_ItWritesAuditRecordsOfMigration_Input")]
            public async Task GivenUserNameWithMatchingOldPasswordCredential_ItWritesAuditRecordsOfMigration(
                Func<Fakes, User> getUser)
            {
                // Arrange
                var user = getUser(_fakes);
                var oldCredentialType = user.Credentials.First().Type;

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                Assert.True(_authenticationService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RemoveCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == oldCredentialType &&
                    ar.AffectedCredential[0].Value == null));

                Assert.True(_authenticationService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.AddCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialBuilder.LatestPasswordType &&
                    ar.AffectedCredential[0].Value == null));
            }

            [Fact]
            public async Task WhenUserLoginFailsAfterFailureUserRecordIsUpdatedWithFailureDetails()
            {
                // Arrange
                var currentTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(currentTime);

                _fakes.User.FailedLoginCount = 7;
                _fakes.User.LastFailedLoginUtc = currentTime - TimeSpan.FromMinutes(1);

                // Act
                await _authenticationService.Authenticate(_fakes.User.Username, "bogus password!!");

                // Assert
                Assert.Equal(currentTime, _fakes.User.LastFailedLoginUtc);
                Assert.Equal(8, _fakes.User.FailedLoginCount);
            }

            [Fact]
            public async Task WhenUserLoginFailsAfterSuccessUserRecordIsUpdatedWithFailureDetails()
            {
                // Arrange
                var currentTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(currentTime);

                _fakes.User.FailedLoginCount = 0;
                _fakes.User.LastFailedLoginUtc = null;

                // Act
                await _authenticationService.Authenticate(_fakes.User.Username, "bogus password!!");

                // Assert
                Assert.Equal(currentTime, _fakes.User.LastFailedLoginUtc);
                Assert.Equal(1, _fakes.User.FailedLoginCount);
            }

            [Fact]
            public async Task WhenUserLoginSucceedsAfterFailureFailureDetailsAreReset()
            {
                // Arrange
                var user = _fakes.User;
                user.FailedLoginCount = 8;
                user.LastFailedLoginUtc = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(user.LastFailedLoginUtc.Value + TimeSpan.FromSeconds(10));

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.Success, result.Result);
                Assert.Same(user, result.AuthenticatedUser.User);
                Assert.Equal(0, user.FailedLoginCount);
                Assert.Null(user.LastFailedLoginUtc);
            }

            [Fact]
            public async Task WhenUserLoginSucceedsAfterSuccessFailureDetailsAreReset()
            {
                // Arrange
                var user = _fakes.User;
                user.FailedLoginCount = 0;
                user.LastFailedLoginUtc = DateTime.UtcNow;

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.Success, result.Result);
                Assert.Same(user, result.AuthenticatedUser.User);
                Assert.Equal(0, user.FailedLoginCount);
                Assert.Null(user.LastFailedLoginUtc);
            }

            [Theory]
            [MemberData("VerifyAccountLockoutTimeCalculation_Data")]
            public async Task VerifyAccountLockoutTimeCalculation(int failureCount, DateTime? lastFailedLoginTime, DateTime currentTime, int expectedLockoutMinutesLeft)
            {
                // Arrange
                var user = _fakes.User;
                user.FailedLoginCount = failureCount;
                user.LastFailedLoginUtc = lastFailedLoginTime;

                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(currentTime);

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                var expectedResult = expectedLockoutMinutesLeft == 0
                    ? PasswordAuthenticationResult.AuthenticationResult.Success
                    : PasswordAuthenticationResult.AuthenticationResult.AccountLocked;

                Assert.Equal(expectedResult, result.Result);
                Assert.Equal(expectedLockoutMinutesLeft, result.LockTimeRemainingMinutes);
            }

            public static IEnumerable<object[]> VerifyAccountLockoutTimeCalculation_Data
            {
                get
                {
                    return new[]
                    {
                        // No failed logins
                        new object[] {0, null, DateTime.UtcNow, 0}, 
                        // Small number of failed logins, no lock required
                        new object[] {1, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 1), 0},
                        new object[] {5, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 1), 0},
                        new object[] {9, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 1), 0},
                        // Initial lockout period
                        new object[] {10, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 1), 1},
                        new object[] {19, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 59), 1},
                        // Exponentially increasing lockout period
                        new object[] {21, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 1), 10},
                        new object[] {25, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 9, 0), 1},
                        new object[] {29, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 5, 30), 5},
                        new object[] {30, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 0, 1), 100},
                        // Lockout expired
                        new object[] {10, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 0, 10, 0), 0},
                        new object[] {20, new DateTime(2016, 9, 30, 0, 0, 0), new DateTime(2016, 9, 30, 1, 40, 0), 0}
                    };
                }
            }
        }

        public class TheGetApiKeyCredentialMethod : TestContainer
        {
            private Fakes _fakes;
            private AuthenticationService _authenticationService;

            public TheGetApiKeyCredentialMethod()
            {
                _fakes = Get<Fakes>();
                _authenticationService = Get<AuthenticationService>();
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public void GivenValidApiKey_ItReturnsCredential(string apiKeyType)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));

                string plaintextValue = GetPlaintextApiKey(apiKeyType, cred, _fakes);

                // Act
                var result = _authenticationService.GetApiKeyCredential(plaintextValue);

                // Assert
                Assert.NotNull(result);
                Assert.Same(cred, result);
            }

            [Theory]
            [InlineData("abc")]
            [InlineData("oy2cshhnw5nmevgh36jajq4opv5nyjnutapjuhl7xb623m")]
            [InlineData("5db11250-7204-458c-a2b8-3fb577b84d2f")]
            public void GivenInvalidApiKey_ItReturnsNull(string apiKeyType)
            {
                // Arrange and Act
                var result = _authenticationService.GetApiKeyCredential(apiKeyType);

                // Assert
                Assert.Null(result);
            }
        }

        public class TheRevokeApiKeyCredentialMethod : TestContainer
        {
            private Fakes _fakes;
            private AuthenticationService _authenticationService;

            public TheRevokeApiKeyCredentialMethod()
            {
                _fakes = Get<Fakes>();
                _authenticationService = Get<AuthenticationService>();
            }

            [Fact]
            public async Task GivenNullApiKeyCredential_ThrowExceptions()
            {
                // Arrange, Act and Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async ()
                    => await _authenticationService.RevokeApiKeyCredential(null, It.IsAny<CredentialRevocationSource>()));
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevocationSource.GitHub, true)]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevocationSource.GitHub, true)]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevocationSource.GitHub, true)]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevocationSource.GitHub, true)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1, CredentialRevocationSource.GitHub, true)]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevocationSource.GitHub, false)]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevocationSource.GitHub, false)]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevocationSource.GitHub, false)]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevocationSource.GitHub, false)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1, CredentialRevocationSource.GitHub, false)]
            public async Task GivenRevocableApiKeyCredential_RevokeCredential(string apiKeyType,
                CredentialRevocationSource revocationSourceKey,
                bool commitChanges)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));

                // Act
                await _authenticationService.RevokeApiKeyCredential(cred, revocationSourceKey);

                // Assert
                Assert.True(cred.HasExpired);
                Assert.Equal(revocationSourceKey, cred.RevocationSourceKey);

                _authenticationService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RevokeCredential &&
                    ar.Username == _fakes.User.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].RevocationSource == Enum.GetName(typeof(CredentialRevocationSource), revocationSourceKey));

                if (commitChanges)
                {
                    _authenticationService.Entities.VerifyCommitChanges();
                }
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public async Task GivenExpiredApiKeyCredential_ThrowExceptions(string apiKeyType)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));
                cred.Key = 10;
                cred.Expires = DateTime.UtcNow.AddDays(-1);

                // Act and Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await _authenticationService.RevokeApiKeyCredential(cred, CredentialRevocationSource.GitHub));
                Assert.Equal($"The API key credential with Key '{cred.Key}' is not revocable.", exception.Message);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public async Task GivenRevokedApiKeyCredential_ThrowExceptions(string apiKeyType)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));
                cred.Key = 10;
                cred.RevocationSourceKey = CredentialRevocationSource.GitHub;

                // Act and Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await _authenticationService.RevokeApiKeyCredential(cred, CredentialRevocationSource.GitHub));
                Assert.Equal($"The API key credential with Key '{cred.Key}' is not revocable.", exception.Message);
            }

            [Theory]
            [InlineData(CredentialTypes.External.MicrosoftAccount)]
            [InlineData(CredentialTypes.External.AzureActiveDirectoryAccount)]
            [InlineData(CredentialTypes.Password.V3)]
            [InlineData(CredentialTypes.Password.Sha1)]
            [InlineData(CredentialTypes.Password.Pbkdf2)]
            public async Task GivenNotApiKeyCredential_ThrowExceptions(string credentialType)
            {
                // Arrange
                Credential cred;
                if (credentialType == CredentialTypes.Password.Sha1)
                {
                    cred = _fakes.ShaUser.Credentials.Single(
                    c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase));
                }
                else if (credentialType == CredentialTypes.Password.Pbkdf2)
                {
                    cred = _fakes.Pbkdf2User.Credentials.Single(
                    c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase));
                }
                cred.Key = 10;

                // Act and Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await _authenticationService.RevokeApiKeyCredential(cred, CredentialRevocationSource.GitHub));
                Assert.Equal($"The API key credential with Key '{cred.Key}' is not revocable.", exception.Message);
            }
        }

        public class TheIsActiveApiKeyCredentialMethod : TestContainer
        {
            private Credential _credential;
            private AuthenticationService _authenticationService;

            public TheIsActiveApiKeyCredentialMethod()
            {
                _credential = new Credential();
                _credential.Type = CredentialTypes.ApiKey.V4;
                _credential.Expires = DateTime.UtcNow.AddDays(1);
                _credential.Scopes = new[] { new Scope("123", NuGetScopes.PackagePushVersion) };
                _credential.RevocationSourceKey = null;

                _authenticationService = Get<AuthenticationService>();
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public void GivenValidApiKeyCredential_ReturnsTrue(string apiKeyType)
            {
                // Arrange
                _credential.Type = apiKeyType;

                // Act and Assert
                Assert.True(_authenticationService.IsActiveApiKeyCredential(_credential));
            }

            [Fact]
            public void GivenNullApiKeyCredential_ReturnsFalse()
            {
                // Arrange, Act and Assert
                Assert.False(_authenticationService.IsActiveApiKeyCredential(null));
            }

            [Theory]
            [InlineData(CredentialTypes.External.MicrosoftAccount)]
            [InlineData(CredentialTypes.External.AzureActiveDirectoryAccount)]
            [InlineData(CredentialTypes.Password.V3)]
            [InlineData(CredentialTypes.Password.Sha1)]
            [InlineData(CredentialTypes.Password.Pbkdf2)]
            public void GivenNotApiKeyCredential_ReturnsFalse(string credentialType)
            {
                // Arrange
                _credential.Type = credentialType;

                // Act and Assert
                Assert.False(_authenticationService.IsActiveApiKeyCredential(_credential));
            }

            [Fact]
            public void GivenExpiredApiKeyCredential_ReturnsFalse()
            {
                // Arrange
                _credential.Expires = DateTime.UtcNow.AddDays(-1);

                // Act and Assert
                Assert.False(_authenticationService.IsActiveApiKeyCredential(_credential));
            }

            [Fact]
            public void GivenNonScopedNotUsedInLastDaysApiKeyCredential_ReturnsFalse()
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ExpirationInDaysForApiKeyV1 = 10;

                _credential.Type = CredentialTypes.ApiKey.V3;
                _credential.Expires = null;
                _credential.Scopes = null;
                // credential was last used < allowed last used
                _credential.LastUsed = DateTime.UtcNow.AddDays(-20);

                // Act and Assert
                Assert.False(_authenticationService.IsActiveApiKeyCredential(_credential));
            }

            [Fact]
            public void GivenRevokedApiKeyCredential_ReturnsFalse()
            {
                // Arrange
                _credential.RevocationSourceKey = CredentialRevocationSource.GitHub;

                // Act and Assert
                Assert.False(_authenticationService.IsActiveApiKeyCredential(_credential));
            }
        }

        public class TheAuthenticateApiKeyMethod : TestContainer
        {
            private Fakes _fakes;
            private AuthenticationService _authenticationService;
            private Mock<IDateTimeProvider> _dateTimeProviderMock;

            public TheAuthenticateApiKeyMethod()
            {
                _fakes = Get<Fakes>();
                _dateTimeProviderMock = GetMock<IDateTimeProvider>();
                _authenticationService = Get<AuthenticationService>();
            }

            [Fact]
            public async Task GivenAnOrganizationApiKeyCredential_ItReturnsNull()
            {
                // Arrange
                var organization = _fakes.Organization;
                var apiKey = TestCredentialHelper.CreateV2ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1));
                apiKey.User = organization;
                organization.Credentials.Add(apiKey);

                // Act
                var result = await _authenticationService.Authenticate(apiKey.ToString());

                // Assert
                Assert.Null(result);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public async Task GivenMatchingApiKeyCredential_ItReturnsTheUserAndMatchingCredential(string apiKeyType)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));

                string plaintextValue = GetPlaintextApiKey(apiKeyType, cred, _fakes);

                // Act
                var result = await _authenticationService.Authenticate(plaintextValue);

                // Assert
                Assert.NotNull(result);
                Assert.Same(_fakes.User, result.User);
                Assert.Same(cred, result.CredentialUsed);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public async Task GivenMatchingApiKeyCredential_ItWritesCredentialLastUsed(string apiKeyType)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));

                var referenceTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(referenceTime);

                Assert.False(cred.LastUsed.HasValue);

                string plaintextValue = GetPlaintextApiKey(apiKeyType, cred, _fakes);

                // Act
                var result = await _authenticationService.Authenticate(plaintextValue);

                // Assert
                Assert.NotNull(result);
                Assert.True(cred.LastUsed == referenceTime);
                Assert.True(cred.LastUsed.HasValue);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public async Task GivenExpiredMatchingApiKeyCredential_ItReturnsNull(string apiKeyType)
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));

                cred.Expires = DateTime.UtcNow.AddDays(-1);

                string plaintextValue = GetPlaintextApiKey(apiKeyType, cred, _fakes);

                // Act
                var result = await _authenticationService.Authenticate(plaintextValue);

                // Assert
                Assert.Null(result);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, true)]
            [InlineData(CredentialTypes.ApiKey.V2, false)]
            [InlineData(CredentialTypes.ApiKey.V3, false)] 
            [InlineData(CredentialTypes.ApiKey.V4, false)]
            public async Task GivenMatchingApiKeyCredentialThatWasLastUsedTooLongAgo_ItReturnsNullAndExpiresTheApiKeyAndWritesAuditRecord(string apiKeyType, bool shouldExpire)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ExpirationInDaysForApiKeyV1 = 10;

                var cred = _fakes.User.Credentials.Single(c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));

                // credential was last used < allowed last used
                cred.LastUsed = DateTime.UtcNow.AddDays(-20);

                var service = Get<AuthenticationService>();
                string plaintextValue = GetPlaintextApiKey(apiKeyType, cred, _fakes);

                // Act
                var result = await service.Authenticate(plaintextValue);

                // Assert

                if (shouldExpire)
                {
                    Assert.Null(result);
                    Assert.True(cred.HasExpired);
                    Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                        ar.Action == AuditedUserAction.ExpireCredential &&
                        ar.Username == _fakes.User.Username));
                }
                else
                {
                    Assert.NotNull(result);
                    Assert.False(cred.HasExpired);
                    Assert.False(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                       ar.Action == AuditedUserAction.ExpireCredential &&
                       ar.Username == _fakes.User.Username));
                }
            }

            [Fact]
            public async Task GivenMatchingV3ApiKeyWithNoScopesThatWasLastUsedTooLongAgo_ItReturnsNullAndExpiresTheApiKeyAndWritesAuditRecord()
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ExpirationInDaysForApiKeyV1 = 10;

                var cred = _fakes.User.Credentials.Single(c => string.Equals(c.Type, CredentialTypes.ApiKey.V3, StringComparison.OrdinalIgnoreCase));
                
                // Clear the scopes list, to simulate a V3 ApiKey that was generated from a V1 ApiKey
                cred.Scopes = new List<Scope>();

                // credential was last used < allowed last used
                cred.LastUsed = DateTime.UtcNow.AddDays(-20);

                var service = Get<AuthenticationService>();
                string plaintextValue = _fakes.ApiKeyV3PlaintextValue;

                // Act
                var result = await service.Authenticate(plaintextValue);

                // Assert
                Assert.Null(result);
                Assert.True(cred.HasExpired);
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.ExpireCredential &&
                    ar.Username == _fakes.User.Username));
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            public async Task GivenMultipleMatchingCredentials_ItThrows(string apiKeyType)
            {
                // Arrange
                var entities = Get<IEntitiesContext>();
                var cred1 = _fakes.User.Credentials.Single(c => string.Equals(c.Type, apiKeyType, StringComparison.OrdinalIgnoreCase));
                cred1.Key = 42;
                var cred2 = new Credential { Key = 43, Type = cred1.Type, Value = cred1.Value };

                var creds = entities.Set<Credential>();
                creds.Add(cred1);
                creds.Add(cred2);

                var plaintextValue = GetPlaintextApiKey(apiKeyType, cred1, _fakes);

                // Act
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _authenticationService.Authenticate(plaintextValue));

                // Assert
                Assert.Equal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultipleMatchingCredentials,
                    Strings.CredentialType_ApiKey,
                    cred1.Key), ex.Message);
            }

            [Theory]
            [InlineData("abc")]
            [InlineData("oy2cshhnw5nmevgh36jajq4opv5nyjnutapjuhl7xb623m")]
            [InlineData("5db11250-7204-458c-a2b8-3fb577b84d2f")]
            public async Task GivenInvalidApiKeyCredential_ItReturnsNullAndWritesAnAuditRecord(string value)
            {
                // Act
                var result = await _authenticationService.Authenticate(value);

                // Assert
                Assert.Null(result);
                Assert.True(_authenticationService.Auditing.WroteRecord<FailedAuthenticatedOperationAuditRecord>(ar =>
                    ar.Action == AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser &&
                    string.IsNullOrEmpty(ar.UsernameOrEmail)));
            }
        }

        private static string GetPlaintextApiKey(string apiKeyType, Credential cred, Fakes fakes)
        {
            string plaintextValue;
            if (apiKeyType == CredentialTypes.ApiKey.V3)
            {
                plaintextValue = fakes.ApiKeyV3PlaintextValue;
            }
            else if (apiKeyType == CredentialTypes.ApiKey.V4)
            {
                plaintextValue = fakes.ApiKeyV4PlaintextValue;
            }
            else
            {
                plaintextValue = cred.Value;
            }

            return plaintextValue;
        }

        public class TheAuthenticateMethod : TestContainer
        {
            private Fakes _fakes;
            private AuthenticationService _authenticationService;
            private Mock<IDateTimeProvider> _dateTimeProviderMock;

            public TheAuthenticateMethod()
            {
                _fakes = Get<Fakes>();
                _dateTimeProviderMock = GetMock<IDateTimeProvider>();
                _authenticationService = Get<AuthenticationService>();
            }

            // We don't normally test exception conditions, but it's really important that
            // this overload is NOT used for Passwords since every call to generate a Password Credential
            // uses a new Salt and thus produces a value that cannot be looked up in the DB. Instead,
            // we must look up the user and then verify the salted password hash.
            [Fact]
            public async Task GivenPasswordCredential_ItThrowsArgumentException()
            {
                // Arrange
                var cred = new CredentialBuilder().CreatePasswordCredential("bogus");

                // Act
                var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _authenticationService.Authenticate(cred));

                // Assert
                Assert.Equal(
                    Strings.PasswordCredentialsCannotBeUsedHere + Environment.NewLine + "Parameter name: credential",
                    ex.Message);
                Assert.Equal("credential", ex.ParamName);
            }

            [Fact]
            public async Task GivenMatchingCredential_ItWritesCredentialLastUsedForMSA()
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(c => c.Type.Contains(CredentialTypes.External.MicrosoftAccount));

                var referenceTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(referenceTime);

                Assert.False(cred.LastUsed.HasValue);

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result = await _authenticationService.Authenticate(TestCredentialHelper.CreateExternalMSACredential(cred.Value));

                // Assert
                Assert.NotNull(result);
                Assert.True(cred.LastUsed == referenceTime);
                Assert.True(cred.LastUsed.HasValue);
            }

            [Fact]
            public async Task GivenMatchingCredential_ItWritesCredentialLastUsedForAAD()
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(c => c.Type.Contains(CredentialTypes.External.AzureActiveDirectoryAccount));

                var referenceTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(referenceTime);

                Assert.False(cred.LastUsed.HasValue);

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result = await _authenticationService.Authenticate(TestCredentialHelper.CreateExternalAADCredential(cred.Value, cred.TenantId));

                // Assert
                Assert.NotNull(result);
                Assert.True(cred.LastUsed == referenceTime);
                Assert.True(cred.LastUsed.HasValue);
            }

            [Fact]
            public async Task GivenExistingCredentialValue_ItDoesNotAuthenticateIncorrectTenant()
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(c => c.Type.Contains(CredentialTypes.External.AzureActiveDirectoryAccount));

                var referenceTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(referenceTime);

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result = await _authenticationService.Authenticate(TestCredentialHelper.CreateExternalAADCredential(cred.Value, "RandomTenant"));

                // Assert
                Assert.Null(result);
            }
        }

        public class TheCreateSessionAsyncMethod : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task GivenAUser_ItCreatesAnOwinAuthenticationTicketForTheUser(bool isDiscontinuedLogin)
            {
                // Arrange
                var credential = new Credential("type", "value");
                var user = new User("testUser") { Credentials = new[] { credential } };
                var context = Fakes.CreateOwinContext();

                var authUser = new AuthenticatedUser(user, credential);

                var passwordConfigMock = new Mock<ILoginDiscontinuationConfiguration>();
                passwordConfigMock
                    .Setup(x => x.IsLoginDiscontinued(authUser))
                    .Returns(isDiscontinuedLogin);

                GetMock<IContentObjectService>()
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(passwordConfigMock.Object);

                // Act
                await Get<AuthenticationService>().CreateSessionAsync(context, authUser);

                // Assert
                var principal = context.Authentication.AuthenticationResponseGrant.Principal;
                var id = principal.Identity;
                Assert.NotNull(principal);
                Assert.NotNull(id);
                Assert.Equal(user.Username, id.Name);
                Assert.Equal(user.Username, principal.GetClaimOrDefault(ClaimTypes.NameIdentifier));
                Assert.Equal(isDiscontinuedLogin, ClaimsExtensions.HasBooleanClaim(principal.Identity as ClaimsIdentity, NuGetClaims.DiscontinuedLogin));
                Assert.Equal(AuthenticationTypes.LocalUser, id.AuthenticationType);
            }

            [Theory]
            [InlineData("MicrosoftAccount")]
            [InlineData("AzureActiveDirectory")]
            public async Task GivenAUser_ItAddsUserLoginClaims(string credType)
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var credential = credentialBuilder.CreateExternalCredential(credType, "value1", "name1 <email1>", "TEST_TENANT1");
                var passwordCredential = credentialBuilder.CreatePasswordCredential("secret_password");
                var user = new User("testUser") { Credentials = new[] { credential, passwordCredential } };
                user.EnableMultiFactorAuthentication = true;
                var context = Fakes.CreateOwinContext();

                var authUser = new AuthenticatedUser(user, credential);

                var passwordConfigMock = new Mock<ILoginDiscontinuationConfiguration>();
                passwordConfigMock
                    .Setup(x => x.IsLoginDiscontinued(authUser))
                    .Returns(false);

                GetMock<IContentObjectService>()
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(passwordConfigMock.Object);

                // Act
                await Get<AuthenticationService>().CreateSessionAsync(context, authUser, wasMultiFactorAuthenticated: true);

                // Assert
                var principal = context.Authentication.AuthenticationResponseGrant.Principal;
                var identity = principal.Identity as ClaimsIdentity;
                Assert.NotNull(principal);
                Assert.NotNull(identity);
                Assert.Equal(user.Username, identity.Name);
                Assert.Equal(user.Username, principal.GetClaimOrDefault(ClaimTypes.NameIdentifier));
                Assert.Equal(9, identity.Claims.Count());
                Assert.False(ClaimsExtensions.HasBooleanClaim(identity, NuGetClaims.DiscontinuedLogin));
                Assert.True(ClaimsExtensions.HasBooleanClaim(identity, NuGetClaims.PasswordLogin));
                Assert.True(ClaimsExtensions.HasBooleanClaim(identity, NuGetClaims.EnabledMultiFactorAuthentication));
                Assert.True(ClaimsExtensions.HasBooleanClaim(identity, NuGetClaims.WasMultiFactorAuthenticated));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task AddsMultiFactorAuthenticationSettingAppropriately(bool wasMultiFactorAuthenticated)
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var credential = credentialBuilder.CreateExternalCredential("foo", "value1", "name1 <email1>", "TEST_TENANT1");
                var user = new User("testUser") { Credentials = new[] { credential } };
                var context = Fakes.CreateOwinContext();

                var authUser = new AuthenticatedUser(user, credential);

                var passwordConfigMock = new Mock<ILoginDiscontinuationConfiguration>();
                passwordConfigMock
                    .Setup(x => x.IsLoginDiscontinued(authUser))
                    .Returns(false);

                GetMock<IContentObjectService>()
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(passwordConfigMock.Object);

                // Act
                await Get<AuthenticationService>().CreateSessionAsync(context, authUser, wasMultiFactorAuthenticated);

                // Assert
                var principal = context.Authentication.AuthenticationResponseGrant.Principal;
                var identity = principal.Identity as ClaimsIdentity;
                Assert.NotNull(principal);
                Assert.NotNull(identity);
                Assert.Equal(wasMultiFactorAuthenticated, ClaimsExtensions.HasBooleanClaim(identity, NuGetClaims.WasMultiFactorAuthenticated));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange                
                var fakes = Get<Fakes>();
                var context = Fakes.CreateOwinContext();

                var credential = fakes.Admin.Credentials.SingleOrDefault(
                    c => string.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));

                var authenticatedUser = new AuthenticatedUser(fakes.Admin, credential);

                var passwordConfigMock = new Mock<ILoginDiscontinuationConfiguration>();
                passwordConfigMock
                    .Setup(x => x.IsLoginDiscontinued(authenticatedUser))
                    .Returns(false);

                GetMock<IContentObjectService>()
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(passwordConfigMock.Object);

                var service = Get<AuthenticationService>();

                // Act
                await service.CreateSessionAsync(context, authenticatedUser);

                // Assert
                var authenticationService = Get<AuthenticationService>();
                Assert.True(authenticationService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.Login &&
                    ar.Username == fakes.Admin.Username));
            }
        }

        public class TheRegisterMethod 
            : TestContainer
        {
            [Fact]
            public async Task GivenPlainTextPassword_ItSaltsHashesAndPassesThru()
            {
                var fakes = Get<Fakes>();

                // Just tests that the obsolete version passes through to the new version
                string password = "thePassword";
                var mock = GetMock<AuthenticationService>();
                // Mock out the new version, we only care that it is called with expected params
                mock.Setup(a => a.Register(
                        fakes.User.Username,
                        fakes.User.EmailAddress,
                        It.Is<Credential>(c => VerifyPasswordHash(
                            c.Value,
                            CredentialBuilder.LatestPasswordType,
                            password)),
                        It.IsAny<bool>()))
                    .CompletesWithNull()
                    .Verifiable();

                // Act
                await mock.Object.Register(fakes.User.Username, fakes.User.EmailAddress, new CredentialBuilder().CreatePasswordCredential(password));

                // Assert
                mock.VerifyAll();
            }

            [Fact]
            public async Task WillThrowIfTheUsernameIsAlreadyInUse()
            {
                // Arrange
                var auth = Get<AuthenticationService>();
                var fakes = Get<Fakes>();

                // Act
                var ex = await AssertEx.Throws<EntityException>(() =>
                    auth.Register(
                        fakes.User.Username,
                        "theEmailAddress",
                        new CredentialBuilder().CreatePasswordCredential("thePassword")));

                // Assert
                Assert.Equal(string.Format(Strings.UsernameNotAvailable, fakes.User.Username), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheEmailAddressIsAlreadyInUse()
            {
                // Arrange
                var auth = Get<AuthenticationService>();
                var fakes = Get<Fakes>();

                // Act
                var ex = await AssertEx.Throws<EntityException>(() =>
                    auth.Register(
                        "newUser",
                        fakes.User.EmailAddress,
                        new CredentialBuilder().CreatePasswordCredential("thePassword")));

                // Assert
                Assert.Equal(string.Format(Strings.EmailAddressBeingUsed, fakes.User.EmailAddress), ex.Message);
            }

            [Fact]
            public async Task WillSaveTheNewUser()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Arrange
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreatePasswordCredential("thePassword"));

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                auth.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task WillSaveTheNewUserAsConfirmedWhenConfigured()
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;

                var auth = Get<AuthenticationService>();

                // Act
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreatePasswordCredential("thePassword"));

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                Assert.True(authUser.User.Confirmed);
                auth.Entities.VerifyCommitChanges();
            }

            [Theory]
            [InlineData("MicrosoftAccount")]
            [InlineData("AzureActiveDirectory")]
            public async Task WillSaveTheNewUserWithExternalCredentialAndMatchEmailAsConfirmed(string credType)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = true;

                var auth = Get<AuthenticationService>();

                // Act
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreateExternalCredential(credType, "blorg", "Bloog"),
                    true);

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                Assert.True(authUser.User.Confirmed);
                Assert.True(string.Equals(authUser.User.EmailAddress, "theEmailAddress"));
                auth.Entities.VerifyCommitChanges();
            }

            [Theory]
            [InlineData("MicrosoftAccount")]
            [InlineData("AzureActiveDirectory")]
            public async Task WillSaveTheNewUserWithExternalCredentialAndNotMatchEmailAsNotConfirmed(string credType)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = true;

                var auth = Get<AuthenticationService>();

                // Act
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreateExternalCredential(credType, "blorg", "Bloog"),
                    false);

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                auth.Entities.VerifyCommitChanges();
                Assert.True(string.Equals(authUser.User.UnconfirmedEmailAddress, "theEmailAddress"));
                Assert.NotNull(authUser.User.EmailConfirmationToken);
                Assert.False(authUser.User.Confirmed);
            }

            [Fact]
            public async Task SetsAConfirmationToken()
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = true;

                var auth = Get<AuthenticationService>();

                // Arrange
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreatePasswordCredential("thePassword"));

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                auth.Entities.VerifyCommitChanges();

                Assert.NotNull(authUser.User.EmailConfirmationToken);
                Assert.False(authUser.User.Confirmed);
            }

            [Fact]
            public async Task SetsCreatedDate()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreatePasswordCredential("thePassword"));

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                auth.Entities.VerifyCommitChanges();

                // Allow for up to 5 secs of time to have elapsed between Create call and now. Should be plenty
                Assert.True((DateTime.UtcNow - authUser.User.CreatedUtc) < TimeSpan.FromSeconds(5));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var authUser = await auth.Register(
                    "newUser",
                    "theEmailAddress",
                    new CredentialBuilder().CreatePasswordCredential("thePassword"));

                // Assert
                Assert.True(auth.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.Register &&
                    ar.Username == "newUser"));
            }
        }

        public class TheTryReplaceCredentialMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsFalseForInvalidUser()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = await service.TryReplaceCredential(user: null, credential: new Credential());

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task ReturnsFalseForInvalidCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("foo");
                var service = Get<AuthenticationService>();

                // Act
                var result = await service.TryReplaceCredential(user, credential: null);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task ReturnsFalseForExistingMatchingCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var credentialBuilder = new CredentialBuilder();
                var cred1 = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "value1", "name1 <email1>", "TEST_TENANT1");
                var user = fakes.CreateUser("foo");
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);
                service.Entities.Credentials.Add(cred1);

                // Act
                var result = await service.TryReplaceCredential(user, cred1);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task ReturnsTrueForReplacingSelfExistingMatchingCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var credentialBuilder = new CredentialBuilder();
                var cred1 = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "value1", "name1 <email1>", "TEST_TENANT1");
                var user = fakes.CreateUser("foo", cred1);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);
                service.Entities.Credentials.Add(cred1);

                // Act
                var result = await service.TryReplaceCredential(user, cred1);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public async Task ReturnsTrueForSuccessfulReplacingExistingCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var credentialBuilder = new CredentialBuilder();
                var cred1 = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "value1", "name1 <email1>", "TEST_TENANT1");
                var cred2 = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "value2", "name1 <email2>", "TEST_TENANT1");
                var user = fakes.CreateUser("foo", cred1);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);
                service.Entities.Credentials.Add(cred1);

                // Act
                var result = await service.TryReplaceCredential(user, cred2);

                // Assert
                Assert.True(result);
                Assert.Equal(new[] { cred2 }, user.Credentials.ToArray());
                Assert.DoesNotContain(cred1, service.Entities.Credentials);
                service.Entities.VerifyCommitChanges();
            }
        }

        public class TheReplaceCredentialMethod : TestContainer
        {
            [Fact]
            public async Task ThrowsExceptionIfNoUserWithProvidedUserName()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var ex = await AssertEx.Throws<InvalidOperationException>(() =>
                    service.ReplaceCredential("definitelyNotARealUser", new Credential()));

                // Assert
                Assert.Equal(Strings.UserNotFound, ex.Message);
            }

            [Fact]
            public async Task GivenAnOrganization_ThrowsInvalidOperationException()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var authService = Get<AuthenticationService>();

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                    await authService.ReplaceCredential("testOrganization", fakes.Organization.Credentials.First());
                });
            }

            [Fact]
            public async Task AddsNewCredentialIfNoneWithSameTypeForUser()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var existingCred = new Credential("foo", "bar");
                var newCred = new Credential("baz", "boz");
                var user = fakes.CreateUser("foo", existingCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user.Username, newCred);

                // Assert
                Assert.Equal(new[] { existingCred, newCred }, user.Credentials.ToArray());
                service.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task ReplacesExistingCredentialIfOneWithSameTypeExistsForUser()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var frozenCred = new Credential("foo", "bar");
                var existingCred = new Credential("baz", "bar");
                var newCred = new Credential("baz", "boz");
                var user = fakes.CreateUser("foo", existingCred, frozenCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user.Username, newCred);

                // Assert
                Assert.Equal(new[] { frozenCred, newCred }, user.Credentials.ToArray());
                Assert.DoesNotContain(existingCred, service.Entities.Credentials);
                service.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task ReplacesAllExternalCredentialsForUser()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var credentialBuilder = new CredentialBuilder();
                var passwordCred = new Credential("password.v3", "password123");
                var cred1 = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "value1", "name1 <email1>", "TEST_TENANT1");
                var cred2 = credentialBuilder.CreateExternalCredential("AzureAccount", "value2", "name2 <email2>", "TEST_TENANT2");
                var newCred = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "value3", "name1 <email2>", "TEST_TENANT2");
                var user = fakes.CreateUser("foo", cred1, cred2, passwordCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user, newCred);

                // Assert
                Assert.Equal(new[] { passwordCred, newCred }, user.Credentials.ToArray());
                Assert.DoesNotContain(cred1, service.Entities.Credentials);
                Assert.DoesNotContain(cred2, service.Entities.Credentials);
                service.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecordRemovingTheOldCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var existingCred = new Credential("baz", "bar");
                var newCred = new Credential("baz", "boz");
                var user = fakes.CreateUser("foo", existingCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user.Username, newCred);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RemoveCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == existingCred.Type &&
                    ar.AffectedCredential[0].Identity == existingCred.Identity &&
                    ar.AffectedCredential[0].Value == existingCred.Value &&
                    ar.AffectedCredential[0].Created == existingCred.Created &&
                    ar.AffectedCredential[0].Expires == existingCred.Expires));
            }

            [Fact]
            public async Task WritesAuditRecordAddingTheNewCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var existingCred = new Credential("foo", "bar");
                var newCred = new Credential("baz", "boz");
                var user = fakes.CreateUser("foo", existingCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user.Username, newCred);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.AddCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == newCred.Type &&
                    ar.AffectedCredential[0].Identity == newCred.Identity &&
                    ar.AffectedCredential[0].Value == null &&
                    ar.AffectedCredential[0].Created == existingCred.Created &&
                    ar.AffectedCredential[0].Expires == existingCred.Expires));
            }
        }

        public class TheResetPasswordWithTokenMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsNullIfUserNotFound()
            {
                // Arrange
                var authService = Get<AuthenticationService>();

                // Act
                var result = await authService.ResetPasswordWithToken("definitelyAFakeUser", "some-token", "new-password");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task ThrowsExceptionIfUserNotConfirmed()
            {
                // Arrange
                var user = new User
                {
                    Username = "tempUser",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act/Assert
                await AssertEx.Throws<InvalidOperationException>(() => authService.ResetPasswordWithToken("tempUser", "some-token", "new-password"));
            }

            [Fact]
            public async Task ResetsPasswordCredential()
            {
                // Arrange
                var oldCred = new CredentialBuilder().CreatePasswordCredential("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    Credentials = new List<Credential> { oldCred }
                };

                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act
                var result = await authService.ResetPasswordWithToken("user", "some-token", "new-password");

                // Assert
                Assert.NotNull(result);
                var newCred = user.Credentials.Single();
                Assert.Same(result, newCred);
                Assert.Equal(CredentialBuilder.LatestPasswordType, newCred.Type);
                Assert.True(VerifyPasswordHash(newCred.Value, CredentialBuilder.LatestPasswordType, "new-password"));
                authService.Entities.VerifyCommitChanges();
                Assert.Equal(0, user.FailedLoginCount);
                Assert.Null(user.LastFailedLoginUtc);
            }


            public static IEnumerable<object[]> ResetsPasswordMigratesPasswordHash_Input
            {
                get
                {
                    return new[]
                    {
                        new object[] {new Func<string, Credential>(TestCredentialHelper.CreateSha1Password)},
                        new object[] {new Func<string, Credential>(TestCredentialHelper.CreatePbkdf2Password)}
                    };
                }
            }

            [Theory, MemberData("ResetsPasswordMigratesPasswordHash_Input")]
            public async Task ResetsPasswordMigratesPasswordHash(Func<string, Credential> oldCredentialBuilder)
            {
                var oldCred = oldCredentialBuilder("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    Credentials = new List<Credential> { oldCred }
                };

                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                var result = await authService.ResetPasswordWithToken("user", "some-token", "new-password");

                // Assert
                Assert.NotNull(result);
                var newCred = user.Credentials.Single();
                Assert.Same(result, newCred);
                Assert.Equal(CredentialBuilder.LatestPasswordType, newCred.Type);
                Assert.True(VerifyPasswordHash(newCred.Value, CredentialBuilder.LatestPasswordType, "new-password"));
                authService.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecordWhenReplacingPasswordCredential()
            {
                // Arrange
                var oldCred = TestCredentialHelper.CreatePbkdf2Password("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    Credentials = new List<Credential> { oldCred }
                };

                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act
                var result = await authService.ResetPasswordWithToken("user", "some-token", "new-password");

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RemoveCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Pbkdf2 &&
                    ar.AffectedCredential[0].Identity == null &&
                    ar.AffectedCredential[0].Value == null));
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.AddCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialBuilder.LatestPasswordType &&
                    ar.AffectedCredential[0].Identity == null &&
                    ar.AffectedCredential[0].Value == null));
            }
        }

        public class TheGeneratePasswordResetTokenMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsUserNotFoundTypeIfUserDoesNotExist()
            {
                // Arrange
                var authService = Get<AuthenticationService>();

                // Act
                var result = await authService.GeneratePasswordResetToken("nobody@nowhere.com", 1440);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PasswordResetResultType.UserNotFound, result.Type);
                Assert.Null(result.User);
            }

            [Fact]
            public async Task ReturnsUserNotConfirmedTypeIfUserIsNotConfirmed()
            {
                // Arrange
                var user = new User("user") { UnconfirmedEmailAddress = "unique@example.com" };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act
                var result = await authService.GeneratePasswordResetToken(user.Username, 1440);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PasswordResetResultType.UserNotConfirmed, result.Type);
                Assert.Same(user, result.User);
            }

            [Fact]
            public async Task SetsPasswordResetTokenUsingEmail()
            {
                // Arrange
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "unique@example.com",
                    PasswordResetToken = null
                };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);
                var currentDate = DateTime.UtcNow;

                // Act
                var result = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PasswordResetResultType.Success, result.Type);
                Assert.Same(user, result.User);
                Assert.NotNull(user.PasswordResetToken);
                Assert.NotEmpty(user.PasswordResetToken);
                Assert.True(user.PasswordResetTokenExpirationDate >= currentDate.AddMinutes(1440));
            }

            [Fact]
            public async Task WithExistingNotYetExpiredTokenReturnsExistingToken()
            {
                // Arrange
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "unique@example.com",
                    PasswordResetToken = "existing-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act
                var result = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PasswordResetResultType.Success, result.Type);
                Assert.Same(user, result.User);
                Assert.Equal("existing-token", user.PasswordResetToken);
            }

            [Fact]
            public async Task WithExistingExpiredTokenReturnsNewToken()
            {
                // Arrange
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "unique@example.com",
                    PasswordResetToken = "existing-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMilliseconds(-1)
                };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);
                var currentDate = DateTime.UtcNow;

                // Act
                var result = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PasswordResetResultType.Success, result.Type);
                Assert.Same(user, result.User);
                Assert.NotEqual("existing-token", user.PasswordResetToken);
                Assert.True(user.PasswordResetTokenExpirationDate >= currentDate.AddMinutes(1440));
            }

            [Fact]
            public async Task WritesAuditRecordWhenGeneratingNewToken()
            {
                // Arrange
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "unique@example.com",
                    PasswordResetToken = null
                };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);
                var currentDate = DateTime.UtcNow;

                // Act
                var returnedUser = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RequestPasswordReset &&
                    ar.Username == user.Username));
            }
        }

        public class TheChangePasswordMethod : TestContainer
        {
            [Fact]
            public async Task GivenInvalidOldPassword_ItReturnsFalseAndDoesNotChangePassword()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", new CredentialBuilder().CreatePasswordCredential(Fakes.Password));
                var authService = Get<AuthenticationService>();

                // Act
                bool result = await authService.ChangePassword(user, "not-the-right-password!", "new-password!");

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task GivenValidOldPassword_ItReturnsTrueAndReplacesPasswordCredential()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", new CredentialBuilder().CreatePasswordCredential(Fakes.Password));
                var authService = Get<AuthenticationService>();

                // Act
                bool result = await authService.ChangePassword(user, Fakes.Password, "new-password!");

                // Assert
                Assert.True(result);

                var credentialValidator = new CredentialValidator();
                Assert.True(credentialValidator.ValidatePasswordCredential(user.Credentials.First(), "new-password!"));
            }

            [Fact]
            public async Task GivenValidOldPassword_ItWritesAnAuditRecordOfTheChange()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", new CredentialBuilder().CreatePasswordCredential(Fakes.Password));
                var authService = Get<AuthenticationService>();

                // Act
                await authService.ChangePassword(user, Fakes.Password, "new-password!");

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RemoveCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialBuilder.LatestPasswordType));
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.AddCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialBuilder.LatestPasswordType));
            }
        }

        public class TheChallengeMethod : TestContainer
        {
            [Fact]
            public void GivenAnUnknownProviderName_ItThrowsInvalidOperationException()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act/Assert
                Assert.Throws<InvalidOperationException>(() => service.Challenge("gabba gabba hey", "http://microsoft.com"));
            }

            [Fact]
            public void GivenADisabledProviderName_ItThrowsInvalidOperationException()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var mock = new Mock<Authenticator>() { CallBase = true };
                var expected = new ViewResult();
                mock.Setup(a => a.Challenge("http://microsoft.com", null)).Returns(expected);
                service.Authenticators.Add("test", mock.Object);

                // Act/Assert
                Assert.Throws<InvalidOperationException>(() => service.Challenge("test", "http://microsoft.com"));
            }

            [Fact]
            public void GivenAnKnownProviderName_ItPassesThroughToProvider()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var mock = new Mock<Authenticator>() { CallBase = true };
                var expected = new ViewResult();
                mock.Setup(a => a.Challenge("http://microsoft.com", null)).Returns(expected);
                mock.Object.BaseConfig.Enabled = true;
                service.Authenticators.Add("test", mock.Object);

                // Act
                var actual = service.Challenge("test", "http://microsoft.com");

                // Assert
                Assert.Same(expected, actual);
            }
        }

        public class TheAddCredentialMethod : TestContainer
        {
            [Fact]
            public async Task AddsTheCredentialToTheDataStore()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();

                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", credentialBuilder.CreatePasswordCredential(Fakes.Password));
                var cred = credentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var authService = Get<AuthenticationService>();

                // Act
                await authService.AddCredential(user, cred);

                // Assert
                Assert.Contains(cred, user.Credentials);
                authService.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task GivenAnOrganization_ThrowsInvalidOperationException()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var credential = new CredentialBuilder().CreatePasswordCredential("password");
                var authService = Get<AuthenticationService>();

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                    await authService.AddCredential(fakes.Organization, credential);
                });
            }

            [Fact]
            public async Task WritesAuditRecordForTheNewCredential()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();

                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", credentialBuilder.CreatePasswordCredential(Fakes.Password));
                var cred = credentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var authService = Get<AuthenticationService>();

                // Act
                await authService.AddCredential(user, cred);

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.AddCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == cred.Type &&
                    ar.AffectedCredential[0].Identity == cred.Identity));
            }
        }

        public class TheDescribeCredentialMethod : TestContainer
        {
            [Fact]
            public void GivenAPasswordCredential_ItDescribesItCorrectly()
            {
                // Arrange
                var cred = new CredentialBuilder().CreatePasswordCredential("wibblejab");
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(Strings.CredentialType_Password, description.TypeCaption);
                Assert.Null(description.Identity);
                Assert.Equal(CredentialKind.Password, description.Kind);
                Assert.Null(description.AuthUI);
            }

            [InlineData(false, false, true)]
            [InlineData(false, true, false)]
            [InlineData(true, true, true)]
            [Theory]
            public void GivenATokenCredential_LegacyApiKey_ItDescribesItCorrectly(bool hasExpired, bool hasBeenUsedInLastDays, bool expectedHasExpired)
            {
                // Arrange
                const int expirationForApiKeyV1 = 365;

                var configurationService = GetConfigurationService();
                configurationService.Current.ExpirationInDaysForApiKeyV1 = expirationForApiKeyV1;

                var cred = TestCredentialHelper.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1);
                cred.LastUsed = hasBeenUsedInLastDays
                    ? DateTime.UtcNow
                    : DateTime.UtcNow - TimeSpan.FromDays(expirationForApiKeyV1 + 1);
                cred.Expires = hasExpired ? DateTime.UtcNow - TimeSpan.FromDays(1) : DateTime.UtcNow + TimeSpan.FromDays(1);

                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(Strings.CredentialType_ApiKey, description.TypeCaption);
                Assert.Null(description.Identity);
                Assert.Equal(cred.Created, description.Created);
                Assert.Equal(cred.Expires, description.Expires);
                Assert.Equal(CredentialKind.Token, description.Kind);
                Assert.Null(description.AuthUI);
                Assert.Equal(Strings.NonScopedApiKeyDescription, description.Description);
                Assert.Equal(expectedHasExpired, description.HasExpired);
                Assert.Null(description.RevocationSource);
            }

            [InlineData(false)]
            [InlineData(true)]
            [Theory]
            public void GivenATokenCredential_ScopedApiKey_ItDescribesItCorrectly(bool hasExpired)
            {
                // Arrange
                var cred = new CredentialBuilder().CreateApiKey(Fakes.ExpirationForApiKeyV1, out string plaintextApiKey);

                cred.User = new User("user");
                cred.Description = "description";
                cred.Scopes = new[] { new Scope("123", NuGetScopes.PackagePushVersion), new Scope("123", NuGetScopes.PackageUnlist) };
                cred.Expires = hasExpired ? DateTime.UtcNow - TimeSpan.FromDays(1) : DateTime.UtcNow + TimeSpan.FromDays(1);
                cred.ExpirationTicks = TimeSpan.TicksPerDay;

                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(Strings.CredentialType_ApiKey, description.TypeCaption);
                Assert.Null(description.Identity);
                Assert.Equal(cred.Created, description.Created);
                Assert.Equal(cred.Expires, description.Expires);
                Assert.Equal(cred.HasExpired, description.HasExpired);
                Assert.Equal(CredentialKind.Token, description.Kind);
                Assert.Null(description.AuthUI);
                Assert.Equal(cred.Description, description.Description);
                Assert.Equal(hasExpired, description.HasExpired);
                Assert.Null(description.RevocationSource);

                Assert.True(description.Scopes.Count == 2);
                Assert.Equal(NuGetScopes.Describe(NuGetScopes.PackagePushVersion), description.Scopes[0].AllowedAction);
                Assert.Equal("123", description.Scopes[0].Subject);
                Assert.Equal(NuGetScopes.Describe(NuGetScopes.PackageUnlist), description.Scopes[1].AllowedAction);
                Assert.Equal("123", description.Scopes[1].Subject);
                Assert.Equal(cred.ExpirationTicks.Value, description.ExpirationDuration.Value.Ticks);
            }

            [InlineData(false)]
            [InlineData(true)]
            [Theory]
            public void GivenAnExternalCredential_ItDescribesItCorrectly(bool hasExpired)
            {
                // Arrange
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                cred.Expires = hasExpired ? DateTime.UtcNow - TimeSpan.FromDays(1) : DateTime.UtcNow + TimeSpan.FromDays(1);

                var msftAuther = new MicrosoftAccountAuthenticator();
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(msftAuther.GetUI().AccountNoun, description.TypeCaption);
                Assert.Equal(cred.Identity, description.Identity);
                Assert.Equal(CredentialKind.External, description.Kind);
                Assert.NotNull(description.AuthUI);
                Assert.Equal(msftAuther.GetUI().AccountNoun, description.AuthUI.AccountNoun);
                Assert.Equal(hasExpired, description.HasExpired);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevocationSource.GitHub)]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevocationSource.GitHub)]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevocationSource.GitHub)]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevocationSource.GitHub)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1, CredentialRevocationSource.GitHub)]
            public void GivenRevokedCredential_ItDescribesItCorrectly(string apiKeyType, CredentialRevocationSource revocationSourceKey)
            {
                // Arrange
                var cred = new Credential(apiKeyType, "TestApiKeyValue");
                cred.RevocationSourceKey = revocationSourceKey;
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(Enum.GetName(typeof(CredentialRevocationSource), revocationSourceKey), description.RevocationSource);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V3)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            [InlineData(CredentialTypes.ApiKey.VerifyV1)]
            public void GivenNotRevokedCredential_ItDescribesItCorrectly(string apiKeyType)
            {
                // Arrange
                var cred = new Credential(apiKeyType, "TestApiKeyValue");
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Null(description.RevocationSource);
            }
        }

        public class TheRemoveCredentialMethod : TestContainer
        {
            [Fact]
            public async Task RemovesTheCredentialFromTheDataStore()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();

                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var user = fakes.CreateUser("test", credentialBuilder.CreatePasswordCredential(Fakes.Password), cred);
                var authService = Get<AuthenticationService>();

                // Act
                await authService.RemoveCredential(user, cred);

                // Assert
                Assert.DoesNotContain(cred, user.Credentials);
                Assert.DoesNotContain(cred, authService.Entities.Credentials);
                authService.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecordForTheRemovedCredential()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();

                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var user = fakes.CreateUser("test", credentialBuilder.CreatePasswordCredential(Fakes.Password), cred);
                var authService = Get<AuthenticationService>();

                // Act
                await authService.RemoveCredential(user, cred);

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.RemoveCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == cred.Type &&
                    ar.AffectedCredential[0].Identity == cred.Identity &&
                    ar.AffectedCredential[0].Value == cred.Value));
            }
        }

        public class TheEditCredentialMethod : TestContainer
        {
            [Fact]
            public async Task SavesChangesInTheDataStore()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();

                var fakes = Get<Fakes>();
                var entities = Get<IEntitiesContext>();

                var cred = credentialBuilder.CreateApiKey(null, out string plaintextApiKey);
                var user = fakes.CreateUser("test", credentialBuilder.CreatePasswordCredential(Fakes.Password), cred);
                var authService = Get<AuthenticationService>();

                var credscopes =
                    Enumerable.Range(0, 5)
                        .Select(
                            i => new Scope { AllowedAction = NuGetScopes.PackagePush, Key = i, Subject = "package" + i }).ToList();

                var newScopes =
                    Enumerable.Range(1, 2)
                        .Select(
                            i => new Scope { AllowedAction = NuGetScopes.PackageUnlist, Key = i * 10, Subject = "otherpackage" + i }).ToList();

                cred.Scopes = credscopes;

                foreach (var scope in credscopes)
                {
                    entities.Scopes.Add(scope);
                }

                // Add an unrelated scope to make sure it's not removed
                entities.Scopes.Add(new Scope { AllowedAction = NuGetScopes.PackagePush, Key = 999, Subject = "package999" });

                // Act
                await authService.EditCredentialScopes(user, cred, newScopes);

                // Assert
                Assert.Equal(1, authService.Entities.Scopes.Count());
                Assert.True(authService.Entities.Scopes.First().Key == 999);

                Assert.Equal(newScopes.Count, cred.Scopes.Count);
                foreach (var newScope in newScopes)
                {
                    Assert.NotNull(cred.Scopes.FirstOrDefault(x => x.Key == newScope.Key));
                }

                authService.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecordForTheEditedCredential()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();

                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreateApiKey(null, out string plaintextApiKey);
                var user = fakes.CreateUser("test", credentialBuilder.CreatePasswordCredential(Fakes.Password), cred);
                var authService = Get<AuthenticationService>();

                // Act
                await authService.EditCredentialScopes(user, cred, new List<Scope>());

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.EditCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == cred.Type));
            }
        }

        public class TheExtractExternalLoginCredentialsMethod : TestContainer
        {
            [Fact]
            public async Task GivenNoExternalCredentials_ItReturnsEmptyResult()
            {
                // Arrange
                var authThunk = new AuthenticateThunk();
                var authService = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                authThunk.Attach(context);

                // Act
                var result = await authService.ReadExternalLoginCredential(context);

                // Assert
                Assert.Null(result.ExternalIdentity);
            }

            [Fact]
            public async Task GivenNoIdClaim_ItReturnsEmptyResult()
            {
                // Arrange
                var authThunk = new AuthenticateThunk()
                {
                    ShimIdentity = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.Name, "blarg", null, "SomeRandomDude")
                    })
                };
                var authService = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                authThunk.Attach(context);

                // Act
                var result = await authService.ReadExternalLoginCredential(context);

                // Assert
                Assert.Null(result.ExternalIdentity);
            }

            [Fact]
            public async Task GivenNoNameClaim_ItReturnsEmptyResult()
            {
                // Arrange
                var authThunk = new AuthenticateThunk()
                {
                    ShimIdentity = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, "blarg", null, "SomeRandomDude")
                    })
                };
                var authService = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                authThunk.Attach(context);

                // Act
                var result = await authService.ReadExternalLoginCredential(context);

                // Assert
                Assert.Null(result.ExternalIdentity);
            }

            [Fact]
            public async Task GivenNoMatchingIssuer_ItReturnsEmptyAuther()
            {
                // Arrange
                var authThunk = new AuthenticateThunk()
                {
                    ShimIdentity = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, "blarg", null, "SomeRandomDude"),
                        new Claim(ClaimTypes.Name, "bloog", null, "SomeRandomDude")
                    })
                };
                var authService = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                authThunk.Attach(context);

                // Act
                var result = await authService.ReadExternalLoginCredential(context);

                // Assert
                Assert.Null(result.ExternalIdentity);
                Assert.Null(result.Authenticator);
            }

            [Fact]
            public async Task GivenMatchingIssuer_ItReturnsTheAutherWithThatName()
            {
                // Arrange
                var authThunk = new AuthenticateThunk()
                {
                    ShimIdentity = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, "blarg", null, "MicrosoftAccount"),
                        new Claim(ClaimTypes.Name, "bloog", null, "MicrosoftAccount")
                    })
                };
                var authService = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                authThunk.Attach(context);

                // Act
                var result = await authService.ReadExternalLoginCredential(context);

                // Assert
                Assert.Same(authThunk.ShimIdentity, result.ExternalIdentity);
                Assert.Same(authService.Authenticators["MicrosoftAccount"], result.Authenticator);
            }

            [Fact]
            public async Task GivenAnIdentity_ItCreatesAnExternalCredential()
            {
                // Arrange
                var authThunk = new AuthenticateThunk()
                {
                    ShimIdentity = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, "blarg", null, "MicrosoftAccount"),
                        new Claim(ClaimTypes.Name, "bloog", null, "MicrosoftAccount")
                    })
                };
                var authService = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                authThunk.Attach(context);

                // Act
                var result = await authService.ReadExternalLoginCredential(context);

                // Assert
                Assert.NotNull(result.Credential);
                Assert.Equal("external.MicrosoftAccount", result.Credential.Type);
                Assert.Equal("blarg", result.Credential.Value);
                Assert.Equal("bloog", result.Credential.Identity);
            }
        }

        public class TheAuthenticateExternalLoginMethod: TestContainer
        {
            [Fact]
            public async Task GivenAnIdentityWithCredential_ItAuthenticates()
            {
                // Arrange
                var context = Fakes.CreateOwinContext();
                var testCredential = new Credential("external.MicrosoftAccount", "blarg");
                testCredential.Identity = "bloog";

                var actualResult = new AuthenticateExternalLoginResult()
                {
                    Credential = testCredential,
                };

                var mock = GetMock<AuthenticationService>();
                mock.Setup(a => a.ReadExternalLoginCredential(It.IsAny<IOwinContext>()))
                    .Returns(Task.FromResult(actualResult))
                    .Verifiable();

                mock.Setup(a => a.Authenticate(testCredential))
                    .Returns(Task.FromResult(new AuthenticatedUser(new User(), testCredential)))
                    .Verifiable();

                // Act
                var result = await mock.Object.AuthenticateExternalLogin(context);

                // Assert
                mock.VerifyAll();
                Assert.NotNull(result.Credential);
                Assert.Equal(testCredential.Type, result.Credential.Type);
                Assert.Equal(testCredential.Value, result.Credential.Value);
                Assert.Equal(testCredential.Identity, result.Credential.Identity);
            }

            [Fact]
            public async Task GivenAnIdentityWithNoCredentialItReturnsResult()
            {
                // Arrange
                var context = Fakes.CreateOwinContext();
                var mock = GetMock<AuthenticationService>();

                mock.Setup(a => a.ReadExternalLoginCredential(It.IsAny<IOwinContext>()))
                    .Returns(Task.FromResult(new AuthenticateExternalLoginResult()))
                    .Verifiable();

                // Act
                var result = await mock.Object.AuthenticateExternalLogin(context);

                // Assert
                mock.VerifyAll();
                Assert.Null(result.Credential);
            }
        }

        private class AuthenticateThunk
        {
            public IIdentity ShimIdentity { get; set; }
            public string[] InvokedAuthTypes { get; private set; }

            public AuthenticateThunk()
            {
            }

            public void Attach(IOwinContext context)
            {
                // Go-go gadget Owin delegate names!
                // This bit of magic basically makes IAuthenticationManager.Authenticate return shimIdentity and some empty dictionaries.
                // If shimIdentity is null, it will just not call the callback and Authenticate will return null
                // It also captures the provided authentication type and stores it

                Func<string[], Action<IIdentity, IDictionary<string, string>, IDictionary<string, object>, object>, object, Task>
                    authenticateThunk = (authenticationTypes, callback, state) =>
                    {
                        InvokedAuthTypes = authenticationTypes;
                        if (ShimIdentity != null)
                        {
                            callback(ShimIdentity, new Dictionary<string, string>(), new Dictionary<string, object>(), state);
                        }
                        return Task.FromResult<object>(null);
                    };
                context.Set<Func<string[], Action<IIdentity, IDictionary<string, string>, IDictionary<string, object>, object>, object, Task>>(
                    "security.Authenticate", authenticateThunk);
            }
        }

        public static bool VerifyPasswordHash(string hash, string algorithm, string password)
        {
            var validator = CredentialValidator.Validators[algorithm];
            bool canAuthenticate = validator(password, new Credential { Value = hash });

            bool sanity = validator("not_the_password", new Credential { Value = hash });

            return canAuthenticate && !sanity;
        }
    }
}