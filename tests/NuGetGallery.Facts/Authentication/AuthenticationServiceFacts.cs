// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
        public class TheAuthenticateMethod : TestContainer
        {
            [Fact]
            public async Task GivenNoUserWithName_ItReturnsNull()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = await service.Authenticate("notARealUser", "password");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task GivenUserNameDoesNotMatchPassword_ItReturnsNull()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = await service.Authenticate(Fakes.User.Username, "bogus password!!");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task GivenUserNameWithMatchingPasswordCredential_ItReturnsAuthenticatedUser()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = await service.Authenticate(Fakes.User.Username, Fakes.Password);

                // Assert
                var expectedCred = Fakes.User.Credentials.SingleOrDefault(
                    c => String.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(result);
                Assert.Same(Fakes.User, result.User);
                Assert.Same(expectedCred, result.CredentialUsed);
            }

            [Fact]
            public async Task GivenUserNameWithMatchingSha1PasswordCredential_ItMigratesHashToPbkdf2()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = await service.Authenticate(Fakes.ShaUser.Username, Fakes.Password);

                // Assert
                var expectedCred = Fakes.User.Credentials.SingleOrDefault(
                    c => String.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(expectedCred);
                Assert.True(VerifyPasswordHash(expectedCred.Value, Constants.PBKDF2HashAlgorithmId, Fakes.Password));
            }

            [Fact]
            public async Task GivenUserNameWithMatchingSha1PasswordCredential_ItWritesAuditRecordsOfMigration()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var user = Fakes.CreateUser("testSha", CredentialBuilder.CreateSha1Password(Fakes.Password));
                service.Entities.Users.Add(user);

                // Act
                var result = await service.Authenticate(user.Username, Fakes.Password);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.RemovedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Sha1 &&
                    ar.AffectedCredential[0].Value == null));
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.AddedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Pbkdf2 &&
                    ar.AffectedCredential[0].Value == null));
            }

            // We don't normally test exception conditions, but it's really important that
            // this overload is NOT used for Passwords since every call to generate a Password Credential
            // uses a new Salt and thus produces a value that cannot be looked up in the DB. Instead,
            // we must look up the user and then verify the salted password hash.
            [Fact]
            public void GivenPasswordCredential_ItThrowsArgumentException()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var cred = CredentialBuilder.CreatePbkdf2Password("bogus");

                // Act
                var ex = Assert.Throws<ArgumentException>(() => service.Authenticate(cred));

                // Assert
                Assert.Equal(Strings.PasswordCredentialsCannotBeUsedHere + Environment.NewLine + "Parameter name: credential", ex.Message);
                Assert.Equal("credential", ex.ParamName);
            }

            [Fact]
            public void GivenInvalidApiKeyCredential_ItReturnsNull()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = service.Authenticate(CredentialBuilder.CreateV1ApiKey());

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void GivenMatchingApiKeyCredential_ItReturnsTheUserAndMatchingCredential()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var cred = Fakes.User.Credentials.Single(
                    c => String.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));
                
                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result = service.Authenticate(CredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value)));

                // Assert
                Assert.NotNull(result);
                Assert.Same(Fakes.User, result.User);
                Assert.Same(cred, result.CredentialUsed);
            }

            [Fact]
            public void GivenMultipleMatchingCredentials_ItThrows()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var entities = Get<IEntitiesContext>();
                var cred = CredentialBuilder.CreateV1ApiKey();
                cred.Key = 42;
                var creds = entities.Set<Credential>();
                creds.Add(cred);
                creds.Add(CredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value)));

                // Act
                var ex = Assert.Throws<InvalidOperationException>(() => service.Authenticate(CredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value))));

                // Assert
                Assert.Equal(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultipleMatchingCredentials,
                    cred.Type,
                    cred.Key), ex.Message);
            }

            [Fact]
            public async Task GivenOnlyASHA1PasswordItAuthenticatesUserAndReplacesItWithAPBKDF2Password()
            {
                var user = Fakes.CreateUser("tempUser", CredentialBuilder.CreateSha1Password("thePassword"));
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                var foundByUserName = await service.Authenticate("tempUser", "thePassword");

                var cred = foundByUserName.User.Credentials.Single();
                Assert.Same(user, foundByUserName.User);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(CryptographyService.ValidateSaltedHash(cred.Value, "thePassword", Constants.PBKDF2HashAlgorithmId));
                service.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task GivenASHA1AndAPBKDF2PasswordItAuthenticatesUserAndRemovesTheSHA1Password()
            {
                var user = Fakes.CreateUser("tempUser", 
                    CredentialBuilder.CreateSha1Password("thePassword"),
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                var foundByUserName = await service.Authenticate("tempUser", "thePassword");

                var cred = foundByUserName.User.Credentials.Single();
                Assert.Same(user, foundByUserName.User);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(CryptographyService.ValidateSaltedHash(cred.Value, "thePassword", Constants.PBKDF2HashAlgorithmId));
                service.Entities.VerifyCommitChanges();
            }
        }

        public class TheCreateSessionMethod : TestContainer
        {
            [Fact]
            public void GivenAUser_ItCreatesAnOwinAuthenticationTicketForTheUser()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var context = Fakes.CreateOwinContext();
                
                var passwordCred = Fakes.Admin.Credentials.SingleOrDefault(
                    c => String.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));

                var authUser = new AuthenticatedUser(Fakes.Admin, passwordCred);

                // Act
                service.CreateSession(context, authUser.User);

                // Assert
                var principal = context.Authentication.AuthenticationResponseGrant.Principal;
                var id = principal.Identity;
                Assert.NotNull(principal);
                Assert.NotNull(id);
                Assert.Equal(Fakes.Admin.Username, id.Name);
                Assert.Equal(Fakes.Admin.Username, principal.GetClaimOrDefault(ClaimTypes.NameIdentifier));
                Assert.Equal(AuthenticationTypes.LocalUser, id.AuthenticationType);
                Assert.True(principal.IsInRole(Constants.AdminRoleName));
            }
        }

        public class TheRegisterMethod : TestContainer
        {
            [Fact]
            public async Task GivenPlainTextPassword_ItSaltsHashesAndPassesThru()
            {
                // Just tests that the obsolete version passes through to the new version
                string password = "thePassword";
                var mock = GetMock<AuthenticationService>();
                // Mock out the new version, we only care that it is called with expected params
                mock.Setup(a => a.Register(
                        Fakes.User.Username,
                        Fakes.User.EmailAddress,
                        It.Is<Credential>(c => VerifyPasswordHash(
                            c.Value,
                            Constants.PBKDF2HashAlgorithmId,
                            password))))
                    .CompletesWithNull()
                    .Verifiable();

                // Act
                await mock.Object.Register(Fakes.User.Username, password, Fakes.User.EmailAddress);

                // Assert
                mock.VerifyAll();
            }

            [Fact]
            public async Task WillThrowIfTheUsernameIsAlreadyInUse()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var ex = await AssertEx.Throws<EntityException>(() =>
                    auth.Register(
                        Fakes.User.Username,
                        "theEmailAddress",
                        CredentialBuilder.CreatePbkdf2Password("thePassword")));

                // Assert
                Assert.Equal(String.Format(Strings.UsernameNotAvailable, Fakes.User.Username), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheEmailAddressIsAlreadyInUse()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var ex = await AssertEx.Throws<EntityException>(() =>
                    auth.Register(
                        "newUser",
                        Fakes.User.EmailAddress,
                        CredentialBuilder.CreatePbkdf2Password("thePassword")));
                
                // Assert
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, Fakes.User.EmailAddress), ex.Message);
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
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));

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
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));

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
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));

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
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));

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
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));

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
                    CredentialBuilder.CreatePbkdf2Password("thePassword"));

                // Assert
                Assert.True(auth.Auditing.WroteRecord<UserAuditRecord>(ar => 
                    ar.Action == UserAuditAction.Registered &&
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
                var existingCred = new Credential("foo", "bar");
                var newCred = new Credential("baz", "boz");
                var user = Fakes.CreateUser("foo", existingCred);
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
                var frozenCred = new Credential("foo", "bar");
                var existingCred = new Credential("baz", "bar");
                var newCred = new Credential("baz", "boz");
                var user = Fakes.CreateUser("foo", existingCred, frozenCred);
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
                var existingCred = new Credential("baz", "bar");
                var newCred = new Credential("baz", "boz");
                var user = Fakes.CreateUser("foo", existingCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user.Username, newCred);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.RemovedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == existingCred.Type &&
                    ar.AffectedCredential[0].Identity == existingCred.Identity &&
                    ar.AffectedCredential[0].Value == existingCred.Value));
            }

            [Fact]
            public async Task WritesAuditRecordAddingTheNewCredential()
            {
                // Arrange
                var existingCred = new Credential("foo", "bar");
                var newCred = new Credential("baz", "boz");
                var user = Fakes.CreateUser("foo", existingCred);
                var service = Get<AuthenticationService>();
                service.Entities.Users.Add(user);

                // Act
                await service.ReplaceCredential(user.Username, newCred);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.AddedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == newCred.Type &&
                    ar.AffectedCredential[0].Identity == newCred.Identity &&
                    ar.AffectedCredential[0].Value == null));
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
                var oldCred = CredentialBuilder.CreatePbkdf2Password("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    Credentials = new List<Credential>() { oldCred }
                };

                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act
                var result = await authService.ResetPasswordWithToken("user", "some-token", "new-password");

                // Assert
                Assert.NotNull(result);
                var newCred = user.Credentials.Single();
                Assert.Same(result, newCred);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, newCred.Type);
                Assert.True(VerifyPasswordHash(newCred.Value, Constants.PBKDF2HashAlgorithmId, "new-password"));
                authService.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task ResetsPasswordMigratesPasswordHash()
            {
                var oldCred = CredentialBuilder.CreateSha1Password("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    Credentials = new List<Credential>() { oldCred }
                };

                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                var result = await authService.ResetPasswordWithToken("user", "some-token", "new-password");

                // Assert
                Assert.NotNull(result);
                var newCred = user.Credentials.Single();
                Assert.Same(result, newCred);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, newCred.Type);
                Assert.True(VerifyPasswordHash(newCred.Value, Constants.PBKDF2HashAlgorithmId, "new-password"));
                authService.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecordWhenReplacingPasswordCredential()
            {
                // Arrange
                var oldCred = CredentialBuilder.CreatePbkdf2Password("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    Credentials = new List<Credential>() { oldCred }
                };

                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                // Act
                var result = await authService.ResetPasswordWithToken("user", "some-token", "new-password");

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.RemovedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Pbkdf2 &&
                    ar.AffectedCredential[0].Identity == null &&
                    ar.AffectedCredential[0].Value == null));
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.AddedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Pbkdf2 &&
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
                    ar.Action == UserAuditAction.RequestedPasswordReset &&
                    ar.Username == user.Username));
            }
        }

        public class TheChangePasswordMethod : TestContainer
        {
            [Fact]
            public async Task GivenInvalidOldPassword_ItReturnsFalseAndDoesNotChangePassword()
            {
                // Arrange
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password));
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
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password));
                var authService = Get<AuthenticationService>();

                // Act
                bool result = await authService.ChangePassword(user, Fakes.Password, "new-password!");

                // Assert
                Assert.True(result);
                
                Credential _;
                Assert.True(AuthenticationService.ValidatePasswordCredential(user.Credentials, "new-password!", out _));
            }

            [Fact]
            public async Task GivenValidOldPassword_ItWritesAnAuditRecordOfTheChange()
            {
                // Arrange
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password));
                var authService = Get<AuthenticationService>();

                // Act
                await authService.ChangePassword(user, Fakes.Password, "new-password!");

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.RemovedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Pbkdf2));
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.AddedCredential &&
                    ar.Username == user.Username &&
                    ar.AffectedCredential.Length == 1 &&
                    ar.AffectedCredential[0].Type == CredentialTypes.Password.Pbkdf2));
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
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password));
                var cred = CredentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
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
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password));
                var cred = CredentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var authService = Get<AuthenticationService>();

                // Act
                await authService.AddCredential(user, cred);

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar => 
                    ar.Action == UserAuditAction.AddedCredential &&
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
                var cred = CredentialBuilder.CreatePbkdf2Password("wibblejab");
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(Strings.CredentialType_Password, description.TypeCaption);
                Assert.Null(description.Identity);
                Assert.True(String.IsNullOrEmpty(description.Value));
                Assert.Equal(CredentialKind.Password, description.Kind);
                Assert.Null(description.AuthUI);
            }

            [Fact]
            public void GivenATokenCredential_ItDescribesItCorrectly()
            {
                // Arrange
                var cred = CredentialBuilder.CreateV1ApiKey(Guid.NewGuid());
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(Strings.CredentialType_ApiKey, description.TypeCaption);
                Assert.Null(description.Identity);
                Assert.Equal(cred.Value, description.Value);
                Assert.Equal(CredentialKind.Token, description.Kind);
                Assert.Null(description.AuthUI);
            }

            [Fact]
            public void GivenAnExternalCredential_ItDescribesItCorrectly()
            {
                // Arrange
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                var msftAuther = new MicrosoftAccountAuthenticator();
                var authService = Get<AuthenticationService>();

                // Act
                var description = authService.DescribeCredential(cred);

                // Assert
                Assert.Equal(cred.Type, description.Type);
                Assert.Equal(msftAuther.GetUI().Caption, description.TypeCaption);
                Assert.Equal(cred.Identity, description.Identity);
                Assert.True(String.IsNullOrEmpty(description.Value));
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
                var cred = CredentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password), cred);
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
                var cred = CredentialBuilder.CreateExternalCredential("flarg", "glarb", "blarb");
                var user = Fakes.CreateUser("test", CredentialBuilder.CreatePbkdf2Password(Fakes.Password), cred);
                var authService = Get<AuthenticationService>();

                // Act
                await authService.RemoveCredential(user, cred);

                // Assert
                Assert.True(authService.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == UserAuditAction.RemovedCredential &&
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
            bool canAuthenticate = CryptographyService.ValidateSaltedHash(
                hash,
                password,
                algorithm);

            bool sanity = CryptographyService.ValidateSaltedHash(
                hash,
                "not_the_password",
                algorithm);

            return canAuthenticate && !sanity;
        }
    }
}