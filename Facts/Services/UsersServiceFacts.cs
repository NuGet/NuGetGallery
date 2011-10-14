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
                Assert.Equal(string.Format(Strings.UsernameNotAvailable, "theUsername"), ex.Message);
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
                Assert.Equal(string.Format(Strings.EmailAddressBeingUsed, "theEmailAddress"), ex.Message);
            }

            [Fact]
            public void WillHasThePassword()
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
                var config = new Mock<IConfiguration>();
                config.Setup(c => c.ConfirmEmailAddresses).Returns(false);
                var userSvc = CreateUsersService(
                    configuration: config,
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
                var configuration = new Mock<IConfiguration>();
                configuration.Setup(c => c.ConfirmEmailAddresses).Returns(false);
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("secret!");
                var userSvc = CreateUsersService(configuration: configuration, cryptoSvc: crypto);

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
                crypto.Setup(c => c.GenerateSaltedHash("new-password", Const.Sha512HashAlgorithmId)).Returns("bacon-hash-and-eggs");
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
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateSaltedHash("new-password", Const.Sha1HashAlgorithmId)).Returns("bacon-hash-and-eggs");
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
                cryptoService.Setup(s => s.ValidateSaltedHash(It.IsAny<string>(), It.IsAny<string>(), Const.Sha512HashAlgorithmId)).Returns(false);
                var service = CreateUsersService(userRepo: userRepository, cryptoSvc: cryptoService);

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.False(changed);
            }

            [Fact]
            public void ReturnsTrueWhenSuccessful()
            {
                var crypto = new CryptographyService();
                var user = new User { Username = "user", HashedPassword = "old hash" };
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var cryptoService = new Mock<ICryptographyService>();
                cryptoService.Setup(s => s.ValidateSaltedHash("old hash", "oldpwd", Const.Sha1HashAlgorithmId)).Returns(true);
                cryptoService.Setup(s => s.GenerateSaltedHash("newpwd", Const.Sha1HashAlgorithmId)).Returns("hash and bacon");
                var service = CreateUsersService(userRepo: userRepository, cryptoSvc: cryptoService);

                var changed = service.ChangePassword("user", "oldpwd", "newpwd");

                Assert.True(changed);
                Assert.Equal("hash and bacon", user.HashedPassword);
            }
        }

        static UserService CreateUsersService(
            Mock<IConfiguration> configuration = null,
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IEntityRepository<User>> userRepo = null,
            Action<Mock<UserService>> setup = null)
        {
            if (configuration == null)
            {
                configuration = new Mock<IConfiguration>();
                configuration.Setup(x => x.ConfirmEmailAddresses).Returns(true);
            }
            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();

            var userSvc = new Mock<UserService>(
                configuration.Object,
                cryptoSvc.Object,
                userRepo.Object);

            userSvc.CallBase = true;

            if (setup != null)
                setup(userSvc);

            return userSvc.Object;
        }
    }
}
