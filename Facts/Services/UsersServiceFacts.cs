using System;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery {
    public class UsersServiceFacts {
        public class TheCreateMethod {
            [Fact]
            public void WillThrowIfTheUsernameIsAlreadyInUse() {
                var userSvc = CreateUsersService(setup: mockUserSvc => {
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
            public void WillThrowIfTheEmailAddressIsAlreadyInUse() {
                var userSvc = CreateUsersService(setup: mockUserSvc => {
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
            public void WillHasThePassword() {
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
            public void WillSaveTheNewUser() {
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
                    u.EmailAddress == "theEmailAddress")));
                userRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void SetsAnApiKey() {
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
            public void SetsAConfirmationToken() {
                var crypto = new Mock<ICryptographyService>();
                crypto.Setup(c => c.GenerateToken()).Returns("secret!");
                var userSvc = CreateUsersService(cryptoSvc: crypto);

                var user = userSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("secret!", user.ConfirmationToken);
                Assert.False(user.Confirmed);
            }
        }

        public class TheGenerateApiKeyMethod {
            [Fact]
            public void SetsApiKeyToNewGuid() {
                var user = new User { ApiKey = Guid.Empty };
                var userSvc = CreateUsersService(setup: mockUserSvc => {
                    mockUserSvc
                        .Setup(x => x.FindByUsername("theUsername"))
                        .Returns(user);
                });

                var apiKey = userSvc.GenerateApiKey("theUsername");

                Assert.NotEqual(Guid.Empty, user.ApiKey);
                Assert.Equal(apiKey, user.ApiKey.ToString());
            }
        }

        public class TheConfirmAccountMethod {
            [Fact]
            public void WithTokenThatDoesNotMatchUserReturnsFalse() {
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(r => r.GetAll()).Returns(new[] { new User() }.AsQueryable());
                var service = CreateUsersService(userRepo: userRepository);

                var confirmed = service.ConfirmAccount("token");

                Assert.False(confirmed);
            }

            [Fact]
            public void WithTokenThatDoesNotMatchUserConfirmsUserAndReturnsTrue() {
                var userRepository = new Mock<IEntityRepository<User>>();
                var user = new User { ConfirmationToken = "secret" };
                userRepository.Setup(r => r.GetAll()).Returns(new[] { user }.AsQueryable());
                var service = CreateUsersService(userRepo: userRepository);

                var confirmed = service.ConfirmAccount("secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
            }

            [Fact]
            public void WithEmptyTokenThrowsArgumentNullException() {
                var service = CreateUsersService();

                Assert.Throws<ArgumentNullException>(() => service.ConfirmAccount(""));
            }
        }

        static UserService CreateUsersService(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IEntityRepository<User>> userRepo = null,
            Action<Mock<UserService>> setup = null) {
            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();

            var userSvc = new Mock<UserService>(
                cryptoSvc.Object,
                userRepo.Object);

            userSvc.CallBase = true;

            if (setup != null)
                setup(userSvc);

            return userSvc.Object;
        }
    }
}
