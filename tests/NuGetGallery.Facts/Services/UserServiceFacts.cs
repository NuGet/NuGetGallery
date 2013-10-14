﻿using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class UserServiceFacts
    {
        public static User CreateAUser(
            string username,
            string emailAddress)
        {
            return CreateAUser(username, password: null, emailAddress: emailAddress);
        }

        public static User CreateAUser(
            string username, 
            string password,
            string emailAddress,
            string hashAlgorithm = Constants.PBKDF2HashAlgorithmId)
        {
            return new User
            {
                Username = username,
                HashedPassword = String.IsNullOrEmpty(password) ? 
                    null : 
                    CryptographyService.GenerateSaltedHash(password, hashAlgorithm),
                PasswordHashAlgorithm = String.IsNullOrEmpty(password) ?
                    null :
                    hashAlgorithm,
                EmailAddress = emailAddress,
            };
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

        public static Credential CreatePasswordCredential(string password)
        {
            return new Credential(
                type: CredentialTypes.Password.Pbkdf2,
                value: CryptographyService.GenerateSaltedHash(
                    password, 
                    Constants.PBKDF2HashAlgorithmId));
        }

        // Now only for things that actually need a MOCK UserService object.
        private static UserService CreateMockUserService(Action<Mock<UserService>> setup, Mock<IEntityRepository<User>> userRepo = null, Mock<IAppConfiguration> config = null)
        {
            if (config == null)
            {
                config = new Mock<IAppConfiguration>();
                config.Setup(x => x.ConfirmEmailAddresses).Returns(true);
            }

            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();
            var credRepo = new Mock<IEntityRepository<Credential>>();

            var userService = new Mock<UserService>(
                config.Object,
                userRepo.Object,
                credRepo.Object)
            {
                CallBase = true
            };

            if (setup != null)
            {
                setup(userService);
            }

            return userService.Object;
        }

        public class TheChangePasswordMethod
        {
            [Fact]
            public void ReturnsFalseIfUserIsNotFound()
            {
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(Enumerable.Empty<User>().AsQueryable());

                var changed = service.ChangePassword("username", "oldpwd", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsFalseIfPasswordDoesNotMatchUser_SHA1()
            {
                var user = new User
                {
                    Username = "user",
                    HashedPassword = CryptographyService.GenerateSaltedHash("oldpwd", "SHA1"),
                    PasswordHashAlgorithm = "SHA1",
                };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "not_the_password", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsFalseIfPasswordDoesNotMatchUser_PBKDF2()
            {
                var user = new User
                {
                    Username = "user",
                    HashedPassword = CryptographyService.GenerateSaltedHash("oldpwd", "PBKDF2"),
                    PasswordHashAlgorithm = "PBKDF2",
                };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user}.AsQueryable());

                var changed = service.ChangePassword("user", "not_the_password", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsFalseIfPasswordDoesNotMatchUser_SHA1Credential()
            {
                var user = new User
                {
                    Username = "user",
                    HashedPassword = "not_the_password",
                    PasswordHashAlgorithm = "SHA1",
                };
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("oldpwd"));
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "not_the_password", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsFalseIfPasswordDoesNotMatchUser_PBKDF2Credential()
            {
                var user = new User
                {
                    Username = "user",
                    HashedPassword = "not_the_password",
                    PasswordHashAlgorithm = "SHA1",
                };
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("oldpwd"));
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "not_the_password", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsTrueWhenSuccessful()
            {
                var hash = CryptographyService.GenerateSaltedHash("oldpwd", "PBKDF2");
                var user = new User { Username = "user", HashedPassword = hash, PasswordHashAlgorithm = "PBKDF2" };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
            }

            [Fact]
            public void UpdatesTheHashedPassword()
            {
                var hash = CryptographyService.GenerateSaltedHash("oldpwd", "PBKDF2");
                var user = new User { Username = "user", HashedPassword = hash, PasswordHashAlgorithm = "PBKDF2" };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");
                Assert.True(VerifyPasswordHash(user.HashedPassword, user.PasswordHashAlgorithm, "newpwd"));
                service.MockUserRepository.VerifyCommitted();
            }

            [Fact]
            public void UpdatesThePasswordCredential()
            {
                var hash = CryptographyService.GenerateSaltedHash("oldpwd", "PBKDF2");
                var user = new User { 
                    Username = "user",
                    Credentials = new List<Credential>()
                    {
                        new Credential(CredentialTypes.Password.Pbkdf2, hash)
                    }
                };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");
                var cred = user.Credentials.Single();
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(VerifyPasswordHash(cred.Value, Constants.PBKDF2HashAlgorithmId, "newpwd"));
                service.MockUserRepository.VerifyCommitted();
            }

            [Fact]
            public void MigratesPasswordIfHashAlgorithmIsNotPBKDF2()
            {
                var user = new User {
                    Username = "user",
                    HashedPassword = CryptographyService.GenerateSaltedHash("oldpwd", "SHA1"), 
                    PasswordHashAlgorithm = "SHA1"
                };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
                Assert.True(VerifyPasswordHash(user.HashedPassword, user.PasswordHashAlgorithm, "newpwd"));
                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                service.MockUserRepository.VerifyCommitted();
            }
        }

        public class TheConfirmEmailAddressMethod
        {
            [Fact]
            public void WithTokenThatDoesNotMatchUserReturnsFalse()
            {
                var user = new User { Username = "username", EmailConfirmationToken = "token" };
                var service = new TestableUserService();

                var confirmed = service.ConfirmEmailAddress(user, "not-token");

                Assert.False(confirmed);
            }

            [Fact]
            public void ThrowsForDuplicateConfirmedEmailAddresses()
            {
                var user = new User { Username = "User1", Key = 1, EmailAddress = "old@example.org", UnconfirmedEmailAddress = "new@example.org", EmailConfirmationToken = "token" };
                var conflictingUser = new User { Username = "User2", Key = 2, EmailAddress = "new@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user, conflictingUser }
                };

                var ex = Assert.Throws<EntityException>(() => service.ConfirmEmailAddress(user, "token"));
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, "new@example.org"), ex.Message);
            }

            [Fact]
            public void WithTokenThatDoesMatchUserConfirmsUserAndReturnsTrue()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = service.ConfirmEmailAddress(user, "secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
                Assert.Equal("new@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public void ForUserWithConfirmedEmailWithTokenThatDoesMatchUserConfirmsUserAndReturnsTrue()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    EmailAddress = "existing@example.com",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = service.ConfirmEmailAddress(user, "secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
                Assert.Equal("new@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public void WithNullUserThrowsArgumentNullException()
            {
                var service = new TestableUserService();

                Assert.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(null, "token"));
            }

            [Fact]
            public void WithEmptyTokenThrowsArgumentNullException()
            {
                var service = new TestableUserService();

                Assert.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(new User(), ""));
            }
        }

        public class TheCreateMethod
        {
            [Fact]
            public void WillThrowIfTheUsernameIsAlreadyInUse()
            {
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByUsername("theUsername"))
                                 .Returns(new User()));

                var ex = Assert.Throws<EntityException>(
                    () =>
                    userService.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
                Assert.Equal(String.Format(Strings.UsernameNotAvailable, "theUsername"), ex.Message);
            }

            [Fact]
            public void WillThrowIfTheEmailAddressIsAlreadyInUse()
            {
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindAllByEmailAddress("theEmailAddress"))
                                 .Returns(new List<User> { new User() }));

                var ex = Assert.Throws<EntityException>(
                    () =>
                    userService.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, "theEmailAddress"), ex.Message);
            }

            [Fact]
            public void WillHashThePassword()
            {
                var userService = new TestableUserService();

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                Assert.True(VerifyPasswordHash(user.HashedPassword, user.PasswordHashAlgorithm, "thePassword"));
            }

            [Fact]
            public void WillSaveTheNewUser()
            {
                var userService = new TestableUserService();


                userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userService.MockUserRepository
                           .Verify(x => x.InsertOnCommit(
                                It.Is<User>(
                                    u =>
                                    u.Username == "theUsername" &&
                                    u.UnconfirmedEmailAddress == "theEmailAddress")));
                userService.MockUserRepository
                           .Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillSaveThePasswordInTheCredentialsTable()
            {
                var userService = new TestableUserService();
                
                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.NotNull(user);
                var passwordCred = user.Credentials.FirstOrDefault(c => c.Type == CredentialTypes.Password.Pbkdf2);
                Assert.NotNull(passwordCred);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, passwordCred.Type);
                Assert.True(VerifyPasswordHash(passwordCred.Value, Constants.PBKDF2HashAlgorithmId, "thePassword"));

                userService.MockUserRepository
                    .Verify(x => x.InsertOnCommit(user));
                userService.MockUserRepository
                    .Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillSaveTheNewUserAsConfirmedWhenConfigured()
            {
                var userService = new TestableUserService();

                userService.MockConfig
                           .Setup(x => x.ConfirmEmailAddresses)
                           .Returns(false);

                userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userService.MockUserRepository
                           .Verify(x => x.InsertOnCommit(
                               It.Is<User>(
                                   u =>
                                   u.Username == "theUsername" &&
                                   u.Confirmed)));
                userService.MockUserRepository
                           .Verify(x => x.CommitChanges());
            }

            [Fact]
            public void SetsAnApiKey()
            {
                var userService = new TestableUserService();

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userService.MockUserRepository
                    .Verify(x => x.InsertOnCommit(user));
                Assert.NotEqual(Guid.Empty, user.ApiKey);

                var apiKeyCred = user.Credentials.FirstOrDefault(c => c.Type == CredentialTypes.ApiKeyV1);
                Assert.NotNull(apiKeyCred);
                Assert.Equal(user.ApiKey.ToString().ToLowerInvariant(), apiKeyCred.Value);
            }

            [Fact]
            public void SetsAConfirmationToken()
            {
                var userService = new TestableUserService();

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.NotEmpty(user.EmailConfirmationToken);
                Assert.False(user.Confirmed);
            }

            [Fact]
            public void SetsCreatedDate()
            {
                var userService = new TestableUserService();

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.NotNull(user.CreatedUtc);

                // Allow for up to 5 secs of time to have elapsed between Create call and now. Should be plenty
                Assert.True((DateTime.UtcNow - user.CreatedUtc) < TimeSpan.FromSeconds(5));
            }

            [Fact]
            public void SetsTheUserToConfirmedWhenEmailConfirmationIsNotEnabled()
            {
                var userService = new TestableUserService();
                userService.MockConfig
                           .Setup(x => x.ConfirmEmailAddresses)
                           .Returns(false);

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal(true, user.Confirmed);
            }
        }

        public class TheFindByEmailAddressMethod
        {
            [Fact]
            public void ReturnsNullIfMultipleMatchesExist()
            {
                var user = new User { Username = "User1", Key = 1, EmailAddress = "new@example.org" };
                var conflictingUser = new User { Username = "User2", Key = 2, EmailAddress = "new@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user, conflictingUser }
                };

                var result = service.FindByEmailAddress("new@example.org");
                Assert.Null(result);
            }
        }

        public class TheFindByUsernameAndPasswordMethod
        {
            [Fact]
            public void FindsUsersByUserName()
            {
                var user = CreateAUser("theUsername", "thePassword", "test@example.com");
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void WillNotFindsUsersByEmailAddress()
            {
                var user = CreateAUser("theUsername", "thePassword", "test@example.com");
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                var foundByEmailAddress = service.FindByUsernameAndPassword("test@example.com", "thePassword");

                Assert.Null(foundByEmailAddress);
            }

            [Fact]
            public void DoesNotReturnUserIfPasswordIsInvalid()
            {
                var user = CreateAUser("theUsername", "thePassword", "test@example.com");
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "theWrongPassword");

                Assert.Null(foundByUserName);
            }

            [Fact]
            public void FindsUserBasedOnPasswordInCredentialsTable()
            {
                var user = CreateAUser("theUsername", "test@example.com");
                user.Credentials.Add(CreatePasswordCredential("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);
                
                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void IfSomehowBothPasswordsExistItFindsUserBasedOnPasswordInCredentialsTable()
            {
                var user = CreateAUser("theUsername", "theWrongPassword", "test@example.com");
                user.Credentials.Add(CreatePasswordCredential("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void GivenASHA1PasswordColumnAndNoCredentialsItAuthenticatesUser()
            {
                var user = CreateAUser("theUsername", "thePassword", "test@example.com", hashAlgorithm: Constants.Sha1HashAlgorithmId);
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                Assert.Same(user, foundByUserName);
                Assert.Empty(user.Credentials);
            }

            [Fact]
            public void GivenAPBKDF2PasswordColumnAndNoCredentialsItAuthenticatesUser()
            {
                var user = CreateAUser("theUsername", "thePassword", "test@example.com", hashAlgorithm: Constants.PBKDF2HashAlgorithmId);
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                Assert.Same(user, foundByUserName);
                Assert.Empty(user.Credentials);
            }

            [Fact]
            public void GivenOnlyASHA1CredentialItAuthenticatesUserAndReplacesItWithAPBKDF2Credential()
            {
                var user = CreateAUser("theUsername", password: null, emailAddress: "test@example.com");
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                var cred = foundByUserName.Credentials.Single();
                Assert.Same(user, foundByUserName);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(CryptographyService.ValidateSaltedHash(cred.Value, "thePassword", Constants.PBKDF2HashAlgorithmId));
            }

            [Fact]
            public void GivenASHA1AndAPBKDF2CredentialItAuthenticatesUserWithEitherCredential()
            {
                var user = CreateAUser("theUsername", password: null, emailAddress: "test@example.com");
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("thePassword1"));
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("thePassword2"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByPassword1 = service.FindByUsernameAndPassword("theUsername", "thePassword1");
                var foundByPassword2 = service.FindByUsernameAndPassword("theUsername", "thePassword2");
                Assert.Same(user, foundByPassword1);
                Assert.Same(foundByPassword1, foundByPassword2);
            }

            [Fact]
            public void GivenASHA1AndAPBKDF2CredentialItAuthenticatesUserAndRemovesTheSHA1Cred()
            {
                var user = CreateAUser("theUsername", password: null, emailAddress: "test@example.com");
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("thePassword"));
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                var cred = foundByUserName.Credentials.Single();
                Assert.Same(user, foundByUserName);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(CryptographyService.ValidateSaltedHash(cred.Value, "thePassword", Constants.PBKDF2HashAlgorithmId));
            }
        }

        public class TheFindByUsernameOrEmailAddressAndPasswordMethod
        {
            [Fact]
            public void FindsUsersByUserName()
            {
                var user = new User
                {
                    Username = "theUsername",
                    HashedPassword = CryptographyService.GenerateSaltedHash("thePassword", Constants.PBKDF2HashAlgorithmId),
                    EmailAddress = "test@example.com",
                    PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                };

                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword");
                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void FindsUsersByEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    HashedPassword = CryptographyService.GenerateSaltedHash("thePassword", Constants.PBKDF2HashAlgorithmId),
                    EmailAddress = "test@example.com",
                    PasswordHashAlgorithm = "PBKDF2"
                };

                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                var foundByEmailAddress = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");
                Assert.NotNull(foundByEmailAddress);
                Assert.Same(user, foundByEmailAddress);
            }

            [Fact]
            public void FindsUserBasedOnPasswordInCredentialsTable()
            {
                var user = CreateAUser("theUsername", "test@example.com");
                user.Credentials.Add(CreatePasswordCredential("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");

                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void IfSomehowBothPasswordsExistItFindsUserBasedOnPasswordInCredentialsTable()
            {
                var user = CreateAUser("theUsername", "theWrongPassword", "test@example.com");
                user.Credentials.Add(CreatePasswordCredential("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");

                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void GivenASHA1PasswordColumnAndNoCredentialsItAuthenticatesUser()
            {
                var user = CreateAUser("test@example.com", "thePassword", "test@example.com", hashAlgorithm: Constants.Sha1HashAlgorithmId);
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");

                Assert.Same(user, foundByUserName);
                Assert.Empty(user.Credentials);
            }

            [Fact]
            public void GivenAPBKDF2PasswordColumnAndNoCredentialsItAuthenticatesUser()
            {
                var user = CreateAUser("test@example.com", "thePassword", "test@example.com", hashAlgorithm: Constants.PBKDF2HashAlgorithmId);
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");

                Assert.Same(user, foundByUserName);
                Assert.Empty(user.Credentials);
            }

            [Fact]
            public void GivenOnlyASHA1CredentialItAuthenticatesUserAndReplacesItWithAPBKDF2Credential()
            {
                var user = CreateAUser("test@example.com", password: null, emailAddress: "test@example.com");
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");

                var cred = foundByUserName.Credentials.Single();
                Assert.Same(user, foundByUserName);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(CryptographyService.ValidateSaltedHash(cred.Value, "thePassword", Constants.PBKDF2HashAlgorithmId));
            }

            [Fact]
            public void GivenASHA1AndAPBKDF2CredentialItAuthenticatesUserWithEitherCredential()
            {
                var user = CreateAUser("test@example.com", password: null, emailAddress: "test@example.com");
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("thePassword1"));
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("thePassword2"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByPassword1 = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword1");
                var foundByPassword2 = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword2");
                Assert.Same(user, foundByPassword1);
                Assert.Same(foundByPassword1, foundByPassword2);
            }

            [Fact]
            public void GivenASHA1AndAPBKDF2CredentialItAuthenticatesUserAndRemovesTheSHA1Cred()
            {
                var user = CreateAUser("theUsername", password: null, emailAddress: "test@example.com");
                user.Credentials.Add(CredentialBuilder.CreateSha1Password("thePassword"));
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("thePassword"));
                var service = new TestableUserService();
                service.MockUserRepository.HasData(user);
                service.MockCredentialRepository.HasData(user.Credentials);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");

                var cred = foundByUserName.Credentials.Single();
                Assert.Same(user, foundByUserName);
                Assert.Equal(CredentialTypes.Password.Pbkdf2, cred.Type);
                Assert.True(CryptographyService.ValidateSaltedHash(cred.Value, "thePassword", Constants.PBKDF2HashAlgorithmId));
            }
        }

        public class TheAuthenticateCredentialMethod
        {
            [Fact]
            public void ReturnsNullIfNoCredentialOfSpecifiedTypeExists()
            {
                // Arrange
                var creds = new List<Credential>() {
                    new Credential("foo", "bar")
                };
                var service = new TestableUserService();
                service.MockCredentialRepository.HasData(creds);

                // Act
                var result = service.AuthenticateCredential(type: "baz", value: "bar");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ReturnsNullIfNoCredentialOfSpecifiedTypeWithSpecifiedValueExists()
            {
                // Arrange
                var creds = new List<Credential>() {
                    new Credential("foo", "bar")
                };
                var service = new TestableUserService();
                service.MockCredentialRepository.HasData(creds);

                // Act
                var result = service.AuthenticateCredential(type: "foo", value: "baz");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ReturnsCredentialIfOneExistsWithSpecifiedTypeAndValue()
            {
                // Arrange
                var creds = new List<Credential>() {
                    new Credential("foo", "bar")
                };
                var service = new TestableUserService();
                service.MockCredentialRepository.HasData(creds);

                // Act
                var result = service.AuthenticateCredential(type: "foo", value: "bar");

                // Assert
                Assert.Same(creds[0], result);
            }
        }

        public class TheReplaceCredentialMethod
        {
            [Fact]
            public void ThrowsExceptionIfNoUserWithProvidedUserName()
            {
                // Arrange
                var users = new List<User>() {
                    new User("foo")
                };
                var service = new TestableUserService();
                service.MockUserRepository.HasData(users);

                // Act
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    service.ReplaceCredential("biz", new Credential()));

                // Assert
                Assert.Equal(Strings.UserNotFound, ex.Message);
            }

            [Fact]
            public void AddsNewCredentialIfNoneWithSameTypeForUser()
            {
                // Arrange
                var existingCred = new Credential("foo", "bar");
                var newCred = new Credential("baz", "boz");
                var users = new List<User>() {
                    new User("foo") { 
                        Credentials = new List<Credential>() {
                            existingCred
                        }
                    }
                };
                var service = new TestableUserService();
                service.MockUserRepository.HasData(users);

                // Act
                service.ReplaceCredential("foo", newCred);

                // Assert
                Assert.Equal(2, users[0].Credentials.Count);
                Assert.Equal(new[] { existingCred, newCred }, users[0].Credentials.ToArray());
                service.MockUserRepository.VerifyCommitted();
            }

            [Fact]
            public void ReplacesExistingCredentialIfOneWithSameTypeExistsForUser()
            {
                // Arrange
                var frozenCred = new Credential("foo", "bar");
                var existingCred = new Credential("baz", "bar");
                var newCred = new Credential("baz", "boz");
                var users = new List<User>() {
                    new User("foo") { 
                        Credentials = new List<Credential>() {
                            existingCred,
                            frozenCred
                        }
                    }
                };
                var service = new TestableUserService();
                service.MockUserRepository.HasData(users);

                // Act
                service.ReplaceCredential("foo", newCred);

                // Assert
                Assert.Equal(2, users[0].Credentials.Count);
                Assert.Equal(new[] { frozenCred, newCred }, users[0].Credentials.ToArray());
                service.MockCredentialRepository
                    .Verify(x => x.DeleteOnCommit(existingCred));
                service.MockUserRepository.VerifyCommitted();
            }
        }

        public class TheGenerateApiKeyMethod
        {
            [Fact]
            public void SetsApiKeyToNewGuid()
            {
                var user = new User { ApiKey = Guid.Empty };
                var userRepo = new Mock<IEntityRepository<User>>();
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByUsername("theUsername"))
                                 .Returns(user),
                    userRepo: userRepo);

                var apiKey = userService.GenerateApiKey("theUsername");

                Assert.NotEqual(Guid.Empty, user.ApiKey);
                Assert.Equal(apiKey, user.ApiKey.ToString());
                userRepo.Verify(r => r.CommitChanges());
            }
        }

        public class TheGeneratePasswordResetTokenMethod
        {
            [Fact]
            public void ReturnsNullIfEmailIsNotFound()
            {
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("email@example.com"))
                                 .Returns((User)null));

                var token = userService.GeneratePasswordResetToken("email@example.com", 1440);
                Assert.Null(token);
            }

            [Fact]
            public void ThrowsExceptionIfUserIsNotConfirmed()
            {
                var user = new User { Username = "user" };
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("user@example.com"))
                                 .Returns(user));

                Assert.Throws<InvalidOperationException>(() => userService.GeneratePasswordResetToken("user@example.com", 1440));
            }

            [Fact]
            public void SetsPasswordResetTokenUsingEmail()
            {
                var user = new User
                {
                    Username = "user", 
                    EmailAddress = "confirmed@example.com", 
                    PasswordResetToken = null
                };
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("email@example.com"))
                                 .Returns(user));
                var currentDate = DateTime.UtcNow;

                var returnedUser = userService.GeneratePasswordResetToken("email@example.com", 1440);

                Assert.Same(user, returnedUser);
                Assert.NotNull(user.PasswordResetToken);
                Assert.NotEmpty(user.PasswordResetToken);
                Assert.True(user.PasswordResetTokenExpirationDate >= currentDate.AddMinutes(1440));
            }

            [Fact]
            public void WithExistingNotYetExpiredTokenReturnsExistingToken()
            {
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "existing-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("user@example.com"))
                                 .Returns(user));

                var returnedUser = userService.GeneratePasswordResetToken("user@example.com", 1440);

                Assert.Same(user, returnedUser);
                Assert.Equal("existing-token", user.PasswordResetToken);
            }

            [Fact]
            public void WithExistingExpiredTokenReturnsNewToken()
            {
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "existing-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMilliseconds(-1)
                };
                var userService = CreateMockUserService(
                    setup: mockUserService =>
                    {
                        mockUserService
                            .Setup(x => x.FindByEmailAddress("user@example.com"))
                            .Returns(user);
                    });
                var currentDate = DateTime.UtcNow;

                var returnedUser = userService.GeneratePasswordResetToken("user@example.com", 1440);

                Assert.Same(user, returnedUser);
                Assert.NotEmpty(user.PasswordResetToken);
                Assert.NotEqual("existing-token", user.PasswordResetToken);
                Assert.True(user.PasswordResetTokenExpirationDate >= currentDate.AddMinutes(1440));
            }
        }

        public class TheResetPasswordWithTokenMethod
        {
            [Fact]
            public void ReturnsFalseIfUserNotFound()
            {
                var userService = new TestableUserService();
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(Enumerable.Empty<User>().AsQueryable());

                bool result = userService.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.False(result);
            }

            [Fact]
            public void ThrowsExceptionIfUserNotConfirmed()
            {
                var user = new User
                {
                    Username = "user",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var userService = new TestableUserService();
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(new[] { user }.AsQueryable());

                Assert.Throws<InvalidOperationException>(() => userService.ResetPasswordWithToken("user", "some-token", "new-password"));
            }

            [Fact]
            public void ResetsPasswordAndPasswordTokenAndPasswordTokenDate()
            {
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    HashedPassword = CryptographyService.GenerateSaltedHash("thePassword", Constants.PBKDF2HashAlgorithmId),
                    PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                };

                var userService = new TestableUserService();
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(new[] { user }.AsQueryable());

                bool result = userService.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                Assert.True(VerifyPasswordHash(user.HashedPassword, user.PasswordHashAlgorithm, "new-password"));
                Assert.Null(user.PasswordResetToken);
                Assert.Null(user.PasswordResetTokenExpirationDate);
                userService.MockUserRepository.VerifyCommitted();
            }

            [Fact]
            public void ResetsPasswordCredential()
            {
                var oldCred = CredentialBuilder.CreatePbkdf2Password("thePassword");
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    HashedPassword = oldCred.Value,
                    PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                    Credentials = new List<Credential>() { oldCred }
                };

                var userService = new TestableUserService();
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(new[] { user }.AsQueryable());

                bool result = userService.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                var newCred = user.Credentials.Single();
                Assert.Equal(CredentialTypes.Password.Pbkdf2, newCred.Type);
                Assert.True(VerifyPasswordHash(newCred.Value, Constants.PBKDF2HashAlgorithmId, "new-password"));
                userService.MockUserRepository.VerifyCommitted();
            }

            [Fact]
            public void ResetsPasswordMigratesPasswordHash()
            {
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    HashedPassword = CryptographyService.GenerateSaltedHash("thePassword", "SHA1"),
                    PasswordHashAlgorithm = "SHA1",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                };
                var userService = new TestableUserService();
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(new[] { user }.AsQueryable());

                bool result = userService.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                Assert.True(VerifyPasswordHash(user.HashedPassword, user.PasswordHashAlgorithm, "new-password"));
                Assert.Null(user.PasswordResetToken);
                Assert.Null(user.PasswordResetTokenExpirationDate);
                userService.MockUserRepository.VerifyCommitted();
            }
        }

        public class TheChangeEmailMethod
        {
            User CreateUser(string username, string password, string emailAddress)
            {
                return new User
                {
                    Username = username,
                    EmailAddress = emailAddress,
                    HashedPassword = CryptographyService.GenerateSaltedHash(password, Constants.PBKDF2HashAlgorithmId),
                    PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId
                };
            }

            [Fact]
            public void SetsUnconfirmedEmailWhenEmailIsChanged()
            {
                var user = CreateUser("Bob", "ThePassword", "old@example.org");
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                service.ChangeEmailAddress(user, "new@example.org");

                Assert.Equal("old@example.org", user.EmailAddress);
                Assert.Equal("new@example.org", user.UnconfirmedEmailAddress);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            /// <summary>
            /// It has to change the pending confirmation token whenever address changes because otherwise you can do
            /// 1. change address, get confirmation email
            /// 2. change email address again to something you don't own
            /// 3. hit confirm and you confirmed an email address you don't own
            /// </summary>
            [Fact]
            public void ModifiesConfirmationTokenWhenEmailAddressChanged()
            {
                var user = new User { EmailAddress = "old@example.com", EmailConfirmationToken = "pending-token" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                service.ChangeEmailAddress(user, "new@example.com");
                Assert.NotNull(user.EmailConfirmationToken);
                Assert.NotEmpty(user.EmailConfirmationToken);
                Assert.NotEqual("pending-token", user.EmailConfirmationToken);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            /// <summary>
            /// It would be annoying if you start seeing pending email changes as a result of NOT changing your email address.
            /// </summary>
            [Fact]
            public void DoesNotModifyAnythingWhenConfirmedEmailAddressNotChanged()
            {
                var user = new User { EmailAddress = "old@example.com", UnconfirmedEmailAddress = null, EmailConfirmationToken = null };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                service.ChangeEmailAddress(user, "old@example.com");
                Assert.True(user.Confirmed);
                Assert.Equal("old@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            /// <summary>
            /// Because it's bad if your confirmation email no longer works because you did a no-op email address change.
            /// </summary>
            [Theory]
            [InlineData("something@else.com")]
            [InlineData(null)]
            public void DoesNotModifyConfirmationTokenWhenUnconfirmedEmailAddressNotChanged(string confirmedEmailAddress)
            {
                var user = new User { 
                    EmailAddress = confirmedEmailAddress,
                    UnconfirmedEmailAddress = "old@example.com", 
                    EmailConfirmationToken = "pending-token" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                service.ChangeEmailAddress(user, "old@example.com");
                Assert.Equal("pending-token", user.EmailConfirmationToken);
            }

            [Fact]
            public void DoesNotLetYouUseSomeoneElsesConfirmedEmailAddress()
            {
                var user = new User { EmailAddress = "old@example.com", Key = 1 };
                var conflictingUser = new User { EmailAddress = "new@example.com", Key = 2 };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user, conflictingUser },
                };

                var e = Assert.Throws<EntityException>(() => service.ChangeEmailAddress(user, "new@example.com"));
                Assert.Equal(string.Format(Strings.EmailAddressBeingUsed, "new@example.com"), e.Message);
                Assert.Equal("old@example.com", user.EmailAddress);
            }
        }

        public class TheUpdateProfileMethod
        {   
            [Fact]
            public void SavesEmailAllowedSetting()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                service.UpdateProfile(user, false);

                Assert.Equal(false, user.EmailAllowed);
                service.MockUserRepository
                       .Verify(r => r.CommitChanges());
            }

            [Fact]
            public void ThrowsArgumentExceptionForNullUser()
            {
                var service = new TestableUserService();

                ContractAssert.ThrowsArgNull(() => service.UpdateProfile(null, emailAllowed: true), "user");
            }
        }

        public class TestableUserService : UserService
        {
            public Mock<IAppConfiguration> MockConfig { get; protected set; }
            public Mock<IEntityRepository<User>> MockUserRepository { get; protected set; }
            public Mock<IEntityRepository<Credential>> MockCredentialRepository { get; protected set; }

            public TestableUserService()
            {
                Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
                UserRepository = (MockUserRepository = new Mock<IEntityRepository<User>>()).Object;
                CredentialRepository = (MockCredentialRepository = new Mock<IEntityRepository<Credential>>()).Object;

                // Set ConfirmEmailAddress to a default of true
                MockConfig.Setup(c => c.ConfirmEmailAddresses).Returns(true);
            }
        }

        public class TestableUserServiceWithDBFaking : UserService
        {
            public Mock<IAppConfiguration> MockConfig { get; protected set; }

            public FakeEntitiesContext FakeEntitiesContext { get; set; }
            
            public IEnumerable<User> Users
            {
                set
                {
                    foreach (User u in value) FakeEntitiesContext.Set<User>().Add(u);
                }
            }
            
            public TestableUserServiceWithDBFaking()
            {
                Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
                UserRepository = new EntityRepository<User>(FakeEntitiesContext = new FakeEntitiesContext());
            }
        }
    }
}
