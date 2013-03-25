using System;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class UserServiceFacts
    {
        // Now only for things that actually need a MOCK UserService object.
        private static UserService CreateMockUserService(Action<Mock<UserService>> setup, Mock<IEntityRepository<User>> userRepo = null, Mock<ICryptographyService> cryptoService = null, Mock<IConfiguration> config = null)
        {
            if (config == null)
            {
                config = new Mock<IConfiguration>();
                config.Setup(x => x.ConfirmEmailAddresses).Returns(true);
            }

            cryptoService = cryptoService ?? new Mock<ICryptographyService>();
            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();
            
            var userService = new Mock<UserService>(
                config.Object,
                cryptoService.Object,
                userRepo.Object)
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
            public void ReturnsFalseIfPasswordDoesNotMatchUser()
            {
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[]
                        {
                            new User { Username = "user", HashedPassword = "hashed" }
                        }.AsQueryable());
                service.MockCrypto
                       .Setup(s => s.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), Constants.Sha512HashAlgorithmId)).Returns(false);

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsTrueWhenSuccessful()
            {
                var user = new User { Username = "user", HashedPassword = "old hash", PasswordHashAlgorithm = "PBKDF2" };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                service.MockCrypto
                       .Setup(s => s.ValidateSaltedHash("old hash", "oldpwd", Constants.PBKDF2HashAlgorithmId)).Returns(true);
                service.MockCrypto
                       .Setup(s => s.GenerateSaltedHash("newpwd", Constants.PBKDF2HashAlgorithmId)).Returns("hash and bacon");

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
                Assert.Equal("hash and bacon", user.HashedPassword);
            }

            [Fact]
            public void MigratesPasswordIfHashAlgorithmIsNotPBKDF2()
            {
                var user = new User { Username = "user", HashedPassword = "old hash", PasswordHashAlgorithm = "SHA1" };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                service.MockCrypto
                       .Setup(s => s.ValidateSaltedHash("old hash", "oldpwd", Constants.Sha1HashAlgorithmId)).Returns(true);
                service.MockCrypto
                       .Setup(s => s.GenerateSaltedHash("oldpwd", Constants.PBKDF2HashAlgorithmId)).Returns("monkey fighting snakes");
                service.MockCrypto
                       .Setup(s => s.GenerateSaltedHash("newpwd", Constants.PBKDF2HashAlgorithmId)).Returns("hash and bacon");

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
                Assert.Equal("hash and bacon", user.HashedPassword);
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
                    setup: u => u.Setup(x => x.FindByEmailAddress("theEmailAddress"))
                                 .Returns(new User()));

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
                userService.MockCrypto
                           .Setup(x => x.GenerateSaltedHash("thePassword", It.IsAny<string>()))
                           .Returns("theHashedPassword");

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("theHashedPassword", user.HashedPassword);
            }

            [Fact]
            public void WillSaveTheNewUser()
            {
                var userService = new TestableUserService();

                userService.MockCrypto
                           .Setup(x => x.GenerateSaltedHash(It.IsAny<string>(), It.IsAny<string>()))
                           .Returns("theHashedPassword");

                userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userService.MockUserRepository
                           .Verify(x => x.InsertOnCommit(
                                It.Is<User>(
                                    u =>
                                    u.Username == "theUsername" &&
                                    u.HashedPassword == "theHashedPassword" &&
                                    u.UnconfirmedEmailAddress == "theEmailAddress")));
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

                userService.MockCrypto
                           .Setup(x => x.GenerateSaltedHash(It.IsAny<string>(), It.IsAny<string>()))
                           .Returns("theHashedPassword");

                userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userService.MockUserRepository
                           .Verify(x => x.InsertOnCommit(
                               It.Is<User>(
                                   u =>
                                   u.Username == "theUsername" &&
                                   u.HashedPassword == "theHashedPassword" &&
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

                Assert.NotEqual(Guid.Empty, user.ApiKey);
            }

            [Fact]
            public void SetsAConfirmationToken()
            {
                var userService = new TestableUserService();
                userService.MockCrypto
                           .Setup(c => c.GenerateToken())
                           .Returns("secret!");

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("secret!", user.EmailConfirmationToken);
                Assert.False(user.Confirmed);
            }

            [Fact]
            public void SetsTheUserToConfirmedWhenEmailConfirmationIsNotEnabled()
            {
                var userService = new TestableUserService();
                userService.MockConfig
                           .Setup(x => x.ConfirmEmailAddresses)
                           .Returns(false);

                userService.MockCrypto
                           .Setup(c => c.GenerateToken())
                           .Returns("secret!");

                var user = userService.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal(true, user.Confirmed);
            }
        }

        public class TheFindByUsernameAndPasswordMethod
        {
            [Fact]
            public void FindsUsersByUserName()
            {
                var user = new User { Username = "theUsername", HashedPassword = "thePassword", EmailAddress = "test@example.com" };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                service.MockCrypto
                       .Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                       .Returns(true);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");

                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void WillNotFindsUsersByEmailAddress()
            {
                var user = new User { Username = "theUsername", HashedPassword = "thePassword", EmailAddress = "test@example.com" };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                service.MockCrypto
                       .Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                       .Returns(true);

                var foundByEmailAddress = service.FindByUsernameAndPassword("test@example.com", "thePassword");

                Assert.Null(foundByEmailAddress);
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
                    HashedPassword = "thePassword",
                    EmailAddress = "test@example.com",
                    PasswordHashAlgorithm = "PBKDF2"
                };

                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                service.MockCrypto
                       .Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                       .Returns(true);

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
                    HashedPassword = "thePassword",
                    EmailAddress = "test@example.com",
                    PasswordHashAlgorithm = "PBKDF2"
                };

                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());

                service.MockCrypto
                       .Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                       .Returns(true);

                var foundByEmailAddress = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");
                Assert.NotNull(foundByEmailAddress);
                Assert.Same(user, foundByEmailAddress);
            }

            [Fact]
            public void FindsUsersUpdatesPasswordIfUsingLegacyHashAlgorithm()
            {
                var user = new User
                {
                    Username = "theUsername",
                    HashedPassword = "theHashedPassword",
                    EmailAddress = "test@example.com",
                    PasswordHashAlgorithm = "SHA1"
                };

                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());
                service.MockUserRepository
                       .Setup(r => r.CommitChanges())
                       .Verifiable();
                service.MockCrypto
                       .Setup(c => c.ValidateSaltedHash("theHashedPassword", "thePassword", "SHA1"))
                       .Returns(true);
                service.MockCrypto
                       .Setup(c => c.GenerateSaltedHash("thePassword", "PBKDF2"))
                       .Returns("theBetterHashedPassword");


                service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");
                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                Assert.Equal("theBetterHashedPassword", user.HashedPassword);
                service.MockUserRepository.Verify(r => r.CommitChanges(), Times.Once());
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
                var cryptoService = new Mock<ICryptographyService>();
                cryptoService.Setup(s => s.GenerateToken()).Returns("reset-token");
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("user@example.com"))
                                 .Returns(user),
                    cryptoService: cryptoService);

                Assert.Throws<InvalidOperationException>(() => userService.GeneratePasswordResetToken("user@example.com", 1440));
            }

            [Fact]
            public void SetsPasswordResetTokenUsingEmail()
            {
                var user = new User { Username = "user", EmailAddress = "confirmed@example.com" };
                var cryptoService = new Mock<ICryptographyService>();
                cryptoService.Setup(s => s.GenerateToken()).Returns("reset-token");
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("email@example.com"))
                                 .Returns(user),
                    cryptoService: cryptoService);
                var currentDate = DateTime.UtcNow;

                var returnedUser = userService.GeneratePasswordResetToken("email@example.com", 1440);

                Assert.Same(user, returnedUser);
                Assert.Equal("reset-token", user.PasswordResetToken);
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
                var cryptoService = new Mock<ICryptographyService>();
                cryptoService.Setup(s => s.GenerateToken()).Throws(new InvalidOperationException("Should not get called"));
                var userService = CreateMockUserService(
                    setup: u => u.Setup(x => x.FindByEmailAddress("user@example.com"))
                                 .Returns(user),
                    cryptoService: cryptoService);

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
                var cryptoService = new Mock<ICryptographyService>();
                cryptoService.Setup(s => s.GenerateToken()).Returns("reset-token");
                var userService = CreateMockUserService(
                    setup: mockUserService =>
                    {
                        mockUserService
                            .Setup(x => x.FindByEmailAddress("user@example.com"))
                            .Returns(user);
                    },
                    cryptoService: cryptoService);
                var currentDate = DateTime.UtcNow;

                var returnedUser = userService.GeneratePasswordResetToken("user@example.com", 1440);

                Assert.Same(user, returnedUser);
                Assert.Equal("reset-token", user.PasswordResetToken);
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
                userService.MockCrypto
                           .Setup(c => c.GenerateSaltedHash("new-password", Constants.Sha512HashAlgorithmId))
                           .Returns("bacon-hash-and-eggs");
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
                    PasswordHashAlgorithm = "PBKDF2"
                };
                var userService = new TestableUserService();
                userService.MockCrypto
                           .Setup(c => c.GenerateSaltedHash("new-password", Constants.PBKDF2HashAlgorithmId))
                           .Returns("bacon-hash-and-eggs");
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(new[] { user }.AsQueryable());

                bool result = userService.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                Assert.Equal("bacon-hash-and-eggs", user.HashedPassword);
                Assert.Null(user.PasswordResetToken);
                Assert.Null(user.PasswordResetTokenExpirationDate);
                userService.MockUserRepository.Verify(u => u.CommitChanges());
            }

            [Fact]
            public void ResetsPasswordMigratesPasswordHash()
            {
                var user = new User
                {
                    Username = "user",
                    EmailAddress = "confirmed@example.com",
                    PasswordResetToken = "some-token",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1),
                    PasswordHashAlgorithm = "SHA1"
                };
                var userService = new TestableUserService();
                userService.MockCrypto
                           .Setup(c => c.GenerateSaltedHash("new-password", "PBKDF2"))
                           .Returns("bacon-hash-and-eggs");
                userService.MockUserRepository
                           .Setup(r => r.GetAll())
                           .Returns(new[] { user }.AsQueryable());


                bool result = userService.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                Assert.Equal("bacon-hash-and-eggs", user.HashedPassword);
                Assert.Null(user.PasswordResetToken);
                Assert.Null(user.PasswordResetTokenExpirationDate);
                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                userService.MockUserRepository
                           .Verify(u => u.CommitChanges());
            }
        }

        public class TheUpdateProfileMethod
        {
            [Fact]
            public void SetsEmailConfirmationWhenEmailAddressChanged()
            {
                var user = new User { EmailAddress = "old@example.com" };
                var service = new TestableUserService();
                service.MockCrypto
                       .Setup(c => c.GenerateToken())
                       .Returns("token");

                service.UpdateProfile(user, "new@example.com", emailAllowed: true);

                Assert.Equal("token", user.EmailConfirmationToken);
            }

            [Fact]
            public void SetsUnconfirmedEmailWhenEmailIsChanged()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());
                service.MockCrypto
                       .Setup(c => c.GenerateToken())
                       .Returns("token");

                service.UpdateProfile(user, "new@example.org", true);

                Assert.Equal("token", user.EmailConfirmationToken);
                Assert.Equal("old@example.org", user.EmailAddress);
                Assert.Equal("new@example.org", user.UnconfirmedEmailAddress);
                service.MockUserRepository
                       .Verify(r => r.CommitChanges());
            }

            [Fact]
            public void DoesNotSetConfirmationTokenWhenEmailAddressNotChanged()
            {
                var user = new User { EmailAddress = "old@example.com" };
                var service = new TestableUserService();
                service.MockCrypto
                       .Setup(c => c.GenerateToken())
                       .Returns("token");

                service.UpdateProfile(user, "old@example.com", emailAllowed: true);

                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public void DoesNotChangeConfirmationTokenButUserHasPendingEmailChange()
            {
                var user = new User { EmailAddress = "old@example.com", EmailConfirmationToken = "pending-token" };
                var service = new TestableUserService();
                service.MockCrypto
                       .Setup(c => c.GenerateToken())
                       .Returns("token");

                service.UpdateProfile(user, "old@example.com", emailAllowed: true);

                Assert.Equal("pending-token", user.EmailConfirmationToken);
            }

            [Fact]
            public void SavesEmailAllowedSetting()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true };
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());
                service.MockCrypto
                       .Setup(c => c.GenerateToken())
                       .Returns("token");

                service.UpdateProfile(user, "old@example.org", false);

                Assert.Equal(false, user.EmailAllowed);
                service.MockUserRepository
                       .Verify(r => r.CommitChanges());
            }

            [Fact]
            public void ThrowsArgumentExceptionForNullUser()
            {
                var service = new TestableUserService();

                ContractAssert.ThrowsArgNull(() => service.UpdateProfile(null, "test@example.com", emailAllowed: true), "user");
            }
        }

        public class TestableUserService : UserService
        {
            public Mock<ICryptographyService> MockCrypto { get; protected set; }
            public Mock<IConfiguration> MockConfig { get; protected set; }
            public Mock<IEntityRepository<User>> MockUserRepository { get; protected set; }

            public TestableUserService()
            {
                Crypto = (MockCrypto = new Mock<ICryptographyService>()).Object;
                Config = (MockConfig = new Mock<IConfiguration>()).Object;
                UserRepository = (MockUserRepository = new Mock<IEntityRepository<User>>()).Object;

                // Set ConfirmEmailAddress to a default of true
                MockConfig.Setup(c => c.ConfirmEmailAddresses).Returns(true);
            }
        }
    }
}