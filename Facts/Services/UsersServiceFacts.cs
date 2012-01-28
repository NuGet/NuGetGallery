using System;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class UsersServiceFacts
    {
        public class TheCreateMethod
        {
            [Fact]
            public void WillThrowIfTheUsernameIsAlreadyInUse()
            {
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByUsername("theUsername"))
                        .Returns(new User());
                });

                var ex = Assert.Throws<EntityException>(() =>
                    userSvc.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
                Assert.Equal(String.Format(Strings.UsernameNotAvailable, "theUsername"), ex.Message);
            }

            [Fact]
            public void WillThrowIfTheEmailAddressIsAlreadyInUse()
            {
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByEmailAddress("theEmailAddress"))
                        .Returns(new User());
                });

                var ex = Assert.Throws<EntityException>(() =>
                    userSvc.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, "theEmailAddress"), ex.Message);
            }

            [Fact]
            public void WillHashThePassword()
            {
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc
                    .Setup(x => x.GenerateSaltedHash("thePassword", It.IsAny<string>()))
                    .Returns("theHashedPassword");
                var userSvc = CreateUsersService(cryptoSvc: cryptoSvc);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("theHashedPassword", user.HashedPassword);
            }

            [Fact]
            public void WillSaveTheNewUser()
            {
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc
                    .Setup(x => x.GenerateSaltedHash(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns("theHashedPassword");
                var userRepo = new Mock<IEntityRepository<User>>();
                var userSvc = CreateUsersService(
                    cryptoSvc: cryptoSvc,
                    userRepo: userRepo);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userRepo.Verify(x => x.InsertOnCommit(It.Is<User>(u =>
                    u.Username == "theUsername" &&
                    u.HashedPassword == "theHashedPassword" &&
                    u.UnconfirmedEmailAddress == "theEmailAddress")));
                userRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillSaveTheNewUserAsConfirmedWhenConfigured()
            {
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc
                    .Setup(x => x.GenerateSaltedHash(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns("theHashedPassword");
                var userRepo = new Mock<IEntityRepository<User>>();
                var settings = new GallerySetting { ConfirmEmailAddresses = false };
                var userSvc = CreateUsersService(
                    settings: settings,
                    cryptoSvc: cryptoSvc,
                    userRepo: userRepo);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userRepo.Verify(x => x.InsertOnCommit(It.Is<User>(u =>
                    u.Username == "theUsername" &&
                    u.HashedPassword == "theHashedPassword" &&
                    u.Confirmed)));
                userRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void SetsAnApiKey()
            {
                var userRepo = new Mock<IEntityRepository<User>>();
                var userSvc = CreateUsersService(
                    userRepo: userRepo);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.NotEqual(Guid.Empty, user.ApiKey);
            }

            [Fact]
            public void SetsAConfirmationToken()
            {
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("secret!");
                var userSvc = CreateUsersService(cryptoSvc: crypto);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("secret!", user.EmailConfirmationToken);
                Assert.False(user.Confirmed);
            }

            [Fact]
            public void SetsTheUserToConfirmedWhenEmailConfirmationIsNotEnabled()
            {
                var settings = new GallerySetting { ConfirmEmailAddresses = false };
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("secret!");
                var userSvc = CreateUsersService(settings: settings, cryptoSvc: crypto);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal(true, user.Confirmed);
            }
        }

        public class TheGenerateApiKeyMethod
        {
            [Fact]
            public void SetsApiKeyToNewGuid()
            {
                var user = new User { ApiKey = Guid.Empty };
                var userRepo = new Mock<IEntityRepository<User>>();
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByUsername("theUsername"))
                        .Returns(user);
                }, userRepo: userRepo);

                var apiKey = userSvc.GenerateApiKey("theUsername");

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
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByEmailAddress("email@example.com"))
                        .Returns((User)null);
                });

                var token = userSvc.GeneratePasswordResetToken("email@example.com", 1440);
                Assert.Null(token);
            }

            [Fact]
            public void ThrowsExceptionIfUserIsNotConfirmed()
            {
                var user = new User { Username = "user" };
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc.Setup(s => s.GenerateToken()).Returns("reset-token");
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByEmailAddress("user@example.com"))
                        .Returns(user);
                }, cryptoSvc: cryptoSvc);

                Assert.Throws<InvalidOperationException>(() => userSvc.GeneratePasswordResetToken("user@example.com", 1440));
            }

            [Fact]
            public void SetsPasswordResetTokenUsingEmail()
            {
                var user = new User { Username = "user", EmailAddress = "confirmed@example.com" };
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc.Setup(s => s.GenerateToken()).Returns("reset-token");
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByEmailAddress("email@example.com"))
                        .Returns(user);
                }, cryptoSvc: cryptoSvc);
                var currentDate = DateTime.UtcNow;

                var returnedUser = userSvc.GeneratePasswordResetToken("email@example.com", 1440);

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
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc.Setup(s => s.GenerateToken()).Throws(new InvalidOperationException("Should not get called"));
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByEmailAddress("user@example.com"))
                        .Returns(user);
                }, cryptoSvc: cryptoSvc);

                var returnedUser = userSvc.GeneratePasswordResetToken("user@example.com", 1440);

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
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc.Setup(s => s.GenerateToken()).Returns("reset-token");
                var userSvc = CreateUsersService(setup: mockUserSvc =>
                {
                    mockUserSvc
                        .Setup(x => x.FindByEmailAddress("user@example.com"))
                        .Returns(user);
                }, cryptoSvc: cryptoSvc);
                var currentDate = DateTime.UtcNow;

                var returnedUser = userSvc.GeneratePasswordResetToken("user@example.com", 1440);

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
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(Enumerable.Empty<User>().AsQueryable());
                var userSvc = CreateUsersService(userRepo: userRepository);

                bool result = userSvc.ResetPasswordWithToken("user", "some-token", "new-password");

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
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateSaltedHash("new-password", Constants.Sha512HashAlgorithmId)).Returns("bacon-hash-and-eggs");
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var userSvc = CreateUsersService(userRepo: userRepository, cryptoSvc: crypto);

                Assert.Throws<InvalidOperationException>(() => userSvc.ResetPasswordWithToken("user", "some-token", "new-password"));
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
                var crypto = new Mock<ICryptographyService>(MockBehavior.Strict);
                crypto.Setup(c => c.GenerateSaltedHash("new-password", Constants.PBKDF2HashAlgorithmId)).Returns("bacon-hash-and-eggs");
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var userSvc = CreateUsersService(userRepo: userRepository, cryptoSvc: crypto);

                bool result = userSvc.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                Assert.Equal("bacon-hash-and-eggs", user.HashedPassword);
                Assert.Null(user.PasswordResetToken);
                Assert.Null(user.PasswordResetTokenExpirationDate);
                userRepository.Verify(u => u.CommitChanges());
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
                var crypto = new Mock<ICryptographyService>(MockBehavior.Strict);
                crypto.Setup(c => c.GenerateSaltedHash("new-password", "PBKDF2")).Returns("bacon-hash-and-eggs");
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var userSvc = CreateUsersService(userRepo: userRepository, cryptoSvc: crypto);

                bool result = userSvc.ResetPasswordWithToken("user", "some-token", "new-password");

                Assert.True(result);
                Assert.Equal("bacon-hash-and-eggs", user.HashedPassword);
                Assert.Null(user.PasswordResetToken);
                Assert.Null(user.PasswordResetTokenExpirationDate);
                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                userRepository.Verify(u => u.CommitChanges());
            }
        }

        public class TheConfirmEmailAddressMethod
        {
            [Fact]
            public void WithTokenThatDoesNotMatchUserReturnsFalse()
            {
                var user = new User { Username = "username", EmailConfirmationToken = "token" };
                var service = CreateUsersService();

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
                var service = CreateUsersService();

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
                var service = CreateUsersService();

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
                var service = CreateUsersService();

                Assert.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(null, "token"));
            }

            [Fact]
            public void WithEmptyTokenThrowsArgumentNullException()
            {
                var service = CreateUsersService();

                Assert.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(new User(), ""));
            }
        }

        public class TheChangePasswordMethod
        {
            [Fact]
            public void ReturnsFalseIfUserIsNotFound()
            {
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(Enumerable.Empty<User>().AsQueryable());
                var service = CreateUsersService(userRepo: userRepository);

                var changed = service.ChangePassword("username", "oldpwd", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsFalseIfPasswordDoesNotMatchUser()
            {
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { 
                    new User { Username = "user", HashedPassword = "hashed" }
                }.AsQueryable());
                var cryptoService = new Mock<ICryptographyService>();
                cryptoService.Setup(s => s.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), Constants.Sha512HashAlgorithmId)).Returns(false);
                var service = CreateUsersService(userRepo: userRepository, cryptoSvc: cryptoService);

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsTrueWhenSuccessful()
            {
                var user = new User { Username = "user", HashedPassword = "old hash", PasswordHashAlgorithm = "PBKDF2" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var cryptoService = new Mock<ICryptographyService>(MockBehavior.Strict);
                cryptoService.Setup(s => s.ValidateSaltedHash("old hash", "oldpwd", Constants.PBKDF2HashAlgorithmId)).Returns(true);
                cryptoService.Setup(s => s.GenerateSaltedHash("newpwd", Constants.PBKDF2HashAlgorithmId)).Returns("hash and bacon");
                var service = CreateUsersService(userRepo: userRepository, cryptoSvc: cryptoService);

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
                Assert.Equal("hash and bacon", user.HashedPassword);
            }

            [Fact]
            public void MigratesPasswordIfHashAlgorithmIsNotPBKDF2()
            {
                var user = new User { Username = "user", HashedPassword = "old hash", PasswordHashAlgorithm = "SHA1" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var cryptoService = new Mock<ICryptographyService>(MockBehavior.Strict);
                cryptoService.Setup(s => s.ValidateSaltedHash("old hash", "oldpwd", Constants.Sha1HashAlgorithmId)).Returns(true);
                cryptoService.Setup(s => s.GenerateSaltedHash("oldpwd", Constants.PBKDF2HashAlgorithmId)).Returns("monkey fighting snakes");
                cryptoService.Setup(s => s.GenerateSaltedHash("newpwd", Constants.PBKDF2HashAlgorithmId)).Returns("hash and bacon");
                var service = CreateUsersService(userRepo: userRepository, cryptoSvc: cryptoService);

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
                Assert.Equal("hash and bacon", user.HashedPassword);
            }
        }

        public class TheUpdateProfileMethod
        {
            [Fact]
            public void SetsEmailConfirmationWhenEmailAddressChanged()
            {
                var user = new User { EmailAddress = "old@example.com" };
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("token");
                var service = CreateUsersService(cryptoSvc: crypto);

                service.UpdateProfile(user, "new@example.com", emailAllowed: true);

                Assert.Equal("token", user.EmailConfirmationToken);
            }

            [Fact]
            public void SetsUnconfirmedEmailWhenEmailIsChanged()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("token");
                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                service.UpdateProfile(user, "new@example.org", true);

                Assert.Equal("token", user.EmailConfirmationToken);
                Assert.Equal("old@example.org", user.EmailAddress);
                Assert.Equal("new@example.org", user.UnconfirmedEmailAddress);
                userRepository.Verify(r => r.CommitChanges());
            }

            [Fact]
            public void DoesNotSetConfirmationTokenWhenEmailAddressNotChanged()
            {
                var user = new User { EmailAddress = "old@example.com" };
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("token");
                var service = CreateUsersService(cryptoSvc: crypto);

                service.UpdateProfile(user, "old@example.com", emailAllowed: true);

                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public void DoesNotChangeConfirmationTokenButUserHasPendingEmailChange()
            {
                var user = new User { EmailAddress = "old@example.com", EmailConfirmationToken = "pending-token" };
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("token");
                var service = CreateUsersService(cryptoSvc: crypto);

                service.UpdateProfile(user, "old@example.com", emailAllowed: true);

                Assert.Equal("pending-token", user.EmailConfirmationToken);
            }

            [Fact]
            public void SavesEmailAllowedSetting()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("token");
                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                service.UpdateProfile(user, "old@example.org", false);

                Assert.Equal(false, user.EmailAllowed);
                userRepository.Verify(r => r.CommitChanges());
            }

            [Fact]
            public void ThrowsArgumentExceptionForNullUser()
            {
                var service = CreateUsersService();

                Assert.Throws<ArgumentNullException>(() => service.UpdateProfile(null, "test@example.com", emailAllowed: true));
            }
        }

        public class TheFindByUsernameAndPasswordMethod
        {
            [Fact]
            public void FindsUsersByUserName()
            {
                var user = new User { Username = "theUsername", HashedPassword = "thePassword", EmailAddress = "test@example.com" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                var foundByUserName = service.FindByUsernameAndPassword("theUsername", "thePassword");
                
                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void WillNotFindsUsersByEmailAddress()
            {
                var user = new User { Username = "theUsername", HashedPassword = "thePassword", EmailAddress = "test@example.com" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                var foundByEmailAddress = service.FindByUsernameAndPassword("test@example.com", "thePassword");

                Assert.Null(foundByEmailAddress);
            }
        }

        public class TheFindByUsernameOrEmailAddressAndPasswordMethod
        {
            [Fact]
            public void FindsUsersByUserName()
            {
                var user = new User { Username = "theUsername", HashedPassword = "thePassword", EmailAddress = "test@example.com", PasswordHashAlgorithm = "PBKDF2" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                var foundByUserName = service.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword");
                Assert.NotNull(foundByUserName);
                Assert.Same(user, foundByUserName);
            }

            [Fact]
            public void FindsUsersByEmailAddress()
            {
                var user = new User { Username = "theUsername", HashedPassword = "thePassword", EmailAddress = "test@example.com", PasswordHashAlgorithm = "PBKDF2" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());

                var crypto = new Mock<ICryptographyService>(MockBehavior.Strict);
                crypto.Setup(c => c.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                var foundByEmailAddress = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");
                Assert.NotNull(foundByEmailAddress);
                Assert.Same(user, foundByEmailAddress);
            }

            [Fact]
            public void FindsUsersUpdatesPasswordIfUsingLegacyHashAlgorithm()
            {
                var user = new User { Username = "theUsername", HashedPassword = "theHashedPassword", EmailAddress = "test@example.com", PasswordHashAlgorithm = "SHA1" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                userRepository.Setup(r => r.CommitChanges()).Verifiable();

                var crypto = new Mock<ICryptographyService>(MockBehavior.Strict);
                crypto.Setup(c => c.ValidateSaltedHash("theHashedPassword", "thePassword", "SHA1")).Returns(true);
                crypto.Setup(c => c.GenerateSaltedHash("thePassword", "PBKDF2")).Returns("theBetterHashedPassword");

                var service = CreateUsersService(cryptoSvc: crypto, userRepo: userRepository);

                var foundByEmailAddress = service.FindByUsernameOrEmailAddressAndPassword("test@example.com", "thePassword");
                Assert.Equal("PBKDF2", user.PasswordHashAlgorithm);
                Assert.Equal("theBetterHashedPassword", user.HashedPassword);
                userRepository.Verify(r => r.CommitChanges(), Times.Once());
            }
        }

        static UserService CreateUsersService(
            GallerySetting settings = null,
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IEntityRepository<User>> userRepo = null,
            Action<Mock<UserService>> setup = null)
        {
            if (settings == null)
            {
                settings = new GallerySetting { ConfirmEmailAddresses = true };
            }
            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();

            var userSvc = new Mock<UserService>(
                settings,
                cryptoSvc.Object,
                userRepo.Object);

            userSvc.CallBase = true;

            if (setup != null)
                setup(userSvc);

            return userSvc.Object;
        }
    }
}