using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
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

        public class TheChangePasswordMethod : TestContainer
        {

            [Fact]
            public async Task ReturnsFalseIfPasswordDoesNotMatchUser_SHA1()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var user = Fakes.CreateUser("tempUser", 
                    CredentialBuilder.CreateSha1Password("oldpwd"));

                // Act
                var changed = await service.ChangePassword(user, "not_the_password", "newpwd");

                // Assert
                Assert.False(changed);
            }

            [Fact]
            public async Task ReturnsFalseIfPasswordDoesNotMatchUser_PBKDF2()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var user = Fakes.CreateUser("tempUser",
                    CredentialBuilder.CreatePbkdf2Password("oldpwd"));
                
                // Act
                var changed = await service.ChangePassword(user, "not_the_password", "newpwd");

                // Assert
                Assert.False(changed);
            }

            [Fact]
            public async Task ReturnsTrueWhenSuccessful()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var user = Fakes.CreateUser(
                    "tempUser",
                    CredentialBuilder.CreateSha1Password("oldpwd"));
                
                // Act
                var changed = await service.ChangePassword(user, "oldpwd", "newpwd");

                // Assert
                Assert.True(changed);
            }

            [Fact]
            public async Task UpdatesThePasswordCredential()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var user = Fakes.CreateUser(
                    "tempUser",
                    CredentialBuilder.CreatePbkdf2Password("oldpwd"));
                
                // Act
                var changed = await service.ChangePassword(user, "oldpwd", "newpwd");

                // Assert
                var cred = user.Credentials.Single();
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(VerifyPasswordHash(cred.Value, Constants.PBKDF2HashAlgorithmId, "newpwd"));
                service.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task MigratesPasswordIfHashAlgorithmIsNotPBKDF2()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var user = Fakes.CreateUser(
                    "tempUser",
                    CredentialBuilder.CreateSha1Password("oldpwd"));
                
                // Act
                var changed = await service.ChangePassword(user, "oldpwd", "newpwd");

                // Assert
                var cred = user.Credentials.Single(c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(VerifyPasswordHash(cred.Value, Constants.PBKDF2HashAlgorithmId, "newpwd"));
                service.Entities.VerifyCommitChanges();
            }
        }

        public class TheRegisterMethod : TestContainer
        {
            [Fact]
            public async Task WillThrowIfTheUsernameIsAlreadyInUse()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var ex = await AssertEx.Throws<EntityException>(() =>
                    auth.Register(
                        Fakes.User.Username,
                        "thePassword",
                        "theEmailAddress"));

                // Assert
                Assert.Equal(String.Format(Strings.UsernameNotAvailable, Fakes.User.Username), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheEmailAddressIsAlreadyInUse()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var ex = await AssertEx.Throws<EntityException>(
                    () =>
                    auth.Register(
                        "theUsername",
                        "thePassword",
                        Fakes.User.EmailAddress));
                
                // Assert
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, Fakes.User.EmailAddress), ex.Message);
            }

            [Fact]
            public async Task WillHashThePasswordWithPBKDF2()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Act
                var authUser = await auth.Register(
                    "aNewUser",
                    "thePassword",
                    "theEmailAddress");

                // Assert
                Credential matched;
                Assert.True(AuthenticationService.ValidatePasswordCredential(authUser.User.Credentials, "thePassword", out matched));
                Assert.Equal(CredentialTypes.Password.Pbkdf2, matched.Type);
            }

            [Fact]
            public async Task WillSaveTheNewUser()
            {
                // Arrange
                var auth = Get<AuthenticationService>();

                // Arrange
                var authUser = await auth.Register(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

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
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

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
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

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
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

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

                // Arrange
                var authUser = await auth.Register(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                // Assert
                Assert.True(auth.Entities.Users.Contains(authUser.User));
                auth.Entities.VerifyCommitChanges();

                // Allow for up to 5 secs of time to have elapsed between Create call and now. Should be plenty
                Assert.True((DateTime.UtcNow - authUser.User.CreatedUtc) < TimeSpan.FromSeconds(5));
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
        }
    }
}