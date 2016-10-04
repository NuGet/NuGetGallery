﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using NuGetGallery.Auditing;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
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
                Assert.Equal(result.Result, PasswordAuthenticationResult.AuthenticationResult.Success);
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
            public async Task GivenInvalidApiKeyCredential_ItReturnsNull()
            {
                // Act
                var result = await _authenticationService.Authenticate(
                    TestCredentialBuilder.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1));

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task WritesAuditRecordWhenGivenInvalidApiKeyCredential()
            {
                // Act
                await _authenticationService.Authenticate(TestCredentialBuilder.CreateV1ApiKey(Guid.NewGuid(), TimeSpan.Zero));

                // Assert
                Assert.True(_authenticationService.Auditing.WroteRecord<FailedAuthenticatedOperationAuditRecord>(ar =>
                    ar.Action == AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser &&
                    string.IsNullOrEmpty(ar.UsernameOrEmail)));
            }

            [Fact]
            public async Task GivenMatchingApiKeyCredential_ItReturnsTheUserAndMatchingCredential()
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result =
                    await
                        _authenticationService.Authenticate(TestCredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value),
                            Fakes.ExpirationForApiKeyV1));

                // Assert
                Assert.NotNull(result);
                Assert.Same(_fakes.User, result.User);
                Assert.Same(cred, result.CredentialUsed);
            }

            [Fact]
            public async Task GivenMatchingCredential_ItWritesCredentialLastUsed()
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));

                var referenceTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(referenceTime);
                
                Assert.False(cred.LastUsed.HasValue);

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result =
                    await
                        _authenticationService.Authenticate(TestCredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value),
                            Fakes.ExpirationForApiKeyV1));

                // Assert
                Assert.NotNull(result);
                Assert.True(cred.LastUsed == referenceTime);
                Assert.True(cred.LastUsed.HasValue);
            }

            [Fact]
            public async Task GivenExpiredMatchingApiKeyCredential_ItReturnsNull()
            {
                // Arrange
                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));

                cred.Expires = DateTime.UtcNow.AddDays(-1);

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result =
                    await
                        _authenticationService.Authenticate(TestCredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value),
                            Fakes.ExpirationForApiKeyV1));

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task
                GivenMatchingApiKeyCredentialThatWasLastUsedTooLongAgo_ItReturnsNullAndExpiresTheApiKeyAndWritesAuditRecord
                ()
            {
                // Arrange
                var config = GetMock<IAppConfiguration>();
                config.SetupGet(m => m.ExpirationInDaysForApiKeyV1).Returns(10);

                var cred = _fakes.User.Credentials.Single(
                    c => string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));

                // credential was last used < allowed last used
                cred.LastUsed = DateTime.UtcNow
                    .AddDays(-20);

                var service = Get<AuthenticationService>();

                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result = await service.Authenticate(
                    TestCredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value), Fakes.ExpirationForApiKeyV1));

                // Assert
                Assert.Null(result);
                Assert.True(cred.HasExpired);
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.ExpireCredential &&
                    ar.Username == _fakes.User.Username));
            }

            [Fact]
            public async Task GivenMultipleMatchingCredentials_ItThrows()
            {
                // Arrange
                var entities = Get<IEntitiesContext>();
                var cred = TestCredentialBuilder.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1);
                cred.Key = 42;
                var creds = entities.Set<Credential>();
                creds.Add(cred);
                creds.Add(TestCredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value), Fakes.ExpirationForApiKeyV1));

                // Act
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await
                        _authenticationService.Authenticate(TestCredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value),
                            Fakes.ExpirationForApiKeyV1)));

                // Assert
                Assert.Equal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultipleMatchingCredentials,
                    cred.Type,
                    cred.Key), ex.Message);
            }

            [Fact]
            public async Task WhenUserLoginFailsUserRecordIsUpdatedWithFailureDetails()
            {
                // Arrange
                var currentTime = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(currentTime);

                _fakes.User.FailedLoginCount = 7;
                _fakes.User.LastFailedLogin = currentTime - TimeSpan.FromMinutes(1);

                // Act
                await _authenticationService.Authenticate(_fakes.User.Username, "bogus password!!");

                // Assert
                Assert.Equal(currentTime, _fakes.User.LastFailedLogin);
                Assert.Equal(8, _fakes.User.FailedLoginCount);
            }

            [Fact]
            public async Task WhenUserLoginSucceedsFailureDetailsAreReset()
            {
                // Arrange
                var user = _fakes.User;
                user.FailedLoginCount = 8;
                user.LastFailedLogin = DateTime.UtcNow;
                _dateTimeProviderMock.SetupGet(x => x.UtcNow).Returns(user.LastFailedLogin.Value + TimeSpan.FromSeconds(10));

                // Act
                var result = await _authenticationService.Authenticate(user.Username, Fakes.Password);

                // Assert
                Assert.Equal(PasswordAuthenticationResult.AuthenticationResult.Success, result.Result);
                Assert.Same(user, result.AuthenticatedUser.User);
                Assert.Equal(0, user.FailedLoginCount);
                Assert.Null(user.LastFailedLogin);
            }

            [Theory]
            [MemberData("VerifyAccountLockoutTimeCalculation_Data")]
            public async Task VerifyAccountLockoutTimeCalculation(int failureCount, DateTime? lastFailedLoginTime, DateTime currentTime, int expectedLockoutMinutesLeft)
            {
                // Arrange
                var user = _fakes.User;
                user.FailedLoginCount = failureCount;
                user.LastFailedLogin = lastFailedLoginTime;

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

        public class TheCreateSessionAsyncMethod : TestContainer
        {
            [Fact]
            public async Task GivenAUser_ItCreatesAnOwinAuthenticationTicketForTheUser()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var fakes = Get<Fakes>();
                var context = Fakes.CreateOwinContext();

                var passwordCred = fakes.Admin.Credentials.SingleOrDefault(
                    c => string.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));

                var authUser = new AuthenticatedUser(fakes.Admin, passwordCred);

                // Act
                await service.CreateSessionAsync(context, authUser);

                // Assert
                var principal = context.Authentication.AuthenticationResponseGrant.Principal;
                var id = principal.Identity;
                Assert.NotNull(principal);
                Assert.NotNull(id);
                Assert.Equal(fakes.Admin.Username, id.Name);
                Assert.Equal(fakes.Admin.Username, principal.GetClaimOrDefault(ClaimTypes.NameIdentifier));
                Assert.Equal(AuthenticationTypes.LocalUser, id.AuthenticationType);
                Assert.True(principal.IsInRole(Constants.AdminRoleName));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var fakes = Get<Fakes>();
                var context = Fakes.CreateOwinContext();

                var credential = fakes.Admin.Credentials.SingleOrDefault(
                    c => string.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));

                var authenticatedUser = new AuthenticatedUser(fakes.Admin, credential);

                // Act
                await service.CreateSessionAsync(context, authenticatedUser);

                // Assert
                var authenticationService = Get<AuthenticationService>();
                Assert.True(authenticationService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.Login &&
                    ar.Username == fakes.Admin.Username));
            }
        }

        public class TheRegisterMethod : TestContainer
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
                            password))))
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
                var auth = Get<AuthenticationService>();
                GetMock<IAppConfiguration>()
                    .Setup(x => x.ConfirmEmailAddresses)
                    .Returns(false);

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

            [Fact]
            public async Task SetsAnApiKey()
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

                var apiKeyCred = authUser.User.Credentials.FirstOrDefault(c => c.Type == CredentialTypes.ApiKeyV1);
                Assert.NotNull(apiKeyCred);
            }

            [Fact]
            public async Task SetsAConfirmationToken()
            {
                // Arrange
                var auth = Get<AuthenticationService>();
                GetMock<IAppConfiguration>()
                    .Setup(c => c.ConfirmEmailAddresses)
                    .Returns(true);

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
                Assert.Null(user.LastFailedLogin);
            }


            public static IEnumerable<object[]> ResetsPasswordMigratesPasswordHash_Input
            {
                get
                {
                    return new[]
                    {
                        new object[] {new Func<string, Credential>(TestCredentialBuilder.CreateSha1Password)},
                        new object[] {new Func<string, Credential>(TestCredentialBuilder.CreatePbkdf2Password)}
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
                var oldCred = TestCredentialBuilder.CreatePbkdf2Password("thePassword");
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
            public async Task ReturnsNullIfEmailIsNotFound()
            {
                // Arrange
                var authService = Get<AuthenticationService>();

                // Act
                var token = await authService.GeneratePasswordResetToken("nobody@nowhere.com", 1440);

                // Assert
                Assert.Null(token);
            }

            [Fact]
            public async Task ThrowsExceptionIfUserIsNotConfirmed()
            {
                // Arrange
                var user = new User("user") { UnconfirmedEmailAddress = "unique@example.com" };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act/Assert
                await AssertEx.Throws<InvalidOperationException>(() => authService.GeneratePasswordResetToken(user.Username, 1440));
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
                var returnedUser = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.Same(user, returnedUser);
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
                var returnedUser = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.Same(user, returnedUser);
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
                var returnedUser = await authService.GeneratePasswordResetToken(user.EmailAddress, 1440);

                // Assert
                Assert.Same(user, returnedUser);
                Assert.NotEmpty(user.PasswordResetToken);
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
                bool result = await authService.ChangePassword(user, "not-the-right-password!", "new-password!", resetApiKey: false);

                // Assert
                Assert.False(result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task GivenValidOldPassword_ItReturnsTrueAndReplacesPasswordCredentialAndApiKeyV1CredentialWhenNeeded(bool resetApiKey)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", new CredentialBuilder().CreatePasswordCredential(Fakes.Password));
                var authService = Get<AuthenticationService>();
                var oldApiKeyV1Credential = user.Credentials.FirstOrDefault(c =>
                    string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));

                // Act
                bool result = await authService.ChangePassword(user, Fakes.Password, "new-password!", resetApiKey: resetApiKey);

                // Assert
                Assert.True(result);

                var credentialValidator = new CredentialValidator();
                Assert.True(credentialValidator.ValidatePasswordCredential(user.Credentials.First(), "new-password!"));

                if (resetApiKey)
                {
                    Assert.NotEqual(oldApiKeyV1Credential, user.Credentials.FirstOrDefault(c =>
                        string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase)));
                }
                else
                {
                    Assert.Equal(oldApiKeyV1Credential, user.Credentials.FirstOrDefault(c =>
                        string.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase)));
                }
            }

            [Fact]
            public async Task GivenValidOldPassword_ItWritesAnAuditRecordOfTheChange()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", new CredentialBuilder().CreatePasswordCredential(Fakes.Password));
                var authService = Get<AuthenticationService>();

                // Act
                await authService.ChangePassword(user, Fakes.Password, "new-password!", resetApiKey: false);

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
                mock.Setup(a => a.Challenge("http://microsoft.com")).Returns(expected);
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
                mock.Setup(a => a.Challenge("http://microsoft.com")).Returns(expected);
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
                Assert.True(string.IsNullOrEmpty(description.Value));
                Assert.Equal(CredentialKind.Password, description.Kind);
                Assert.Null(description.AuthUI);
            }

            [Fact]
            public void GivenATokenCredential_ItDescribesItCorrectly()
            {
                // Arrange
                var cred = new CredentialBuilder().CreateApiKey(Fakes.ExpirationForApiKeyV1);
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(Strings.CredentialType_ApiKey, description.TypeCaption);
                Assert.Null(description.Identity);
                Assert.Equal(cred.Value, description.Value);
                Assert.Equal(cred.Created, description.Created);
                Assert.Equal(cred.Expires, description.Expires);
                Assert.Equal(cred.HasExpired, description.HasExpired);
                Assert.Equal(CredentialKind.Token, description.Kind);
                Assert.Null(description.AuthUI);
            }

            [Fact]
            public void GivenAnExternalCredential_ItDescribesItCorrectly()
            {
                // Arrange
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                var msftAuther = new MicrosoftAccountAuthenticator();
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(msftAuther.GetUI().Caption, description.TypeCaption);
                Assert.Equal(cred.Identity, description.Identity);
                Assert.True(string.IsNullOrEmpty(description.Value));
                Assert.Equal(CredentialKind.External, description.Kind);
                Assert.NotNull(description.AuthUI);
                Assert.Equal(msftAuther.GetUI().AccountNoun, description.AuthUI.AccountNoun);
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
                Assert.Same(authThunk.ShimIdentity, result.ExternalIdentity);
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