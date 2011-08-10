using System;
using Moq;
using Xunit;

namespace NuGetGallery {
    public class UsersServiceFacts {
        public class The_Create_method {
            [Fact]
            public void will_throw_if_the_username_is_already_in_use() {
                var usersSvc = CreateUsersService(setup: mockUsersSvc => {
                        mockUsersSvc
                            .Setup(x => x.FindByUsername("theUsername"))
                            .Returns(new User());
                    });

                var ex = Assert.Throws<EntityException>(() =>
                    usersSvc.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
                Assert.Equal(string.Format(Strings.UsernameNotAvailable, "theUsername"), ex.Message);
            }

            [Fact]
            public void will_throw_if_the_email_address_is_already_in_use() {
                var usersSvc = CreateUsersService(setup: mockUsersSvc => {
                    mockUsersSvc
                        .Setup(x => x.FindByEmailAddress("theEmailAddress"))
                        .Returns(new User());
                });

                var ex = Assert.Throws<EntityException>(() =>
                    usersSvc.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
                Assert.Equal(string.Format(Strings.EmailAddressBeingUsed, "theEmailAddress"), ex.Message);
            }

            [Fact]
            public void will_hash_the_password() {
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc
                    .Setup(x => x.GenerateSaltedHash("thePassword", It.IsAny<string>()))
                    .Returns("theHashedPassword");
                var usersSvc = CreateUsersService(cryptoSvc: cryptoSvc);

                var user = usersSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                Assert.Equal("theHashedPassword", user.HashedPassword);
            }

            [Fact]
            public void will_save_the_new_user() {
                var cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc
                    .Setup(x => x.GenerateSaltedHash(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns("theHashedPassword");
                var userRepo = new Mock<IEntityRepository<User>>();
                var usersSvc = CreateUsersService(
                    cryptoSvc: cryptoSvc,
                    userRepo: userRepo);

                var user = usersSvc.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress");

                userRepo.Verify(x => x.InsertOnCommit(It.Is<User>(u =>
                    u.Username == "theUsername" &&
                    u.HashedPassword == "theHashedPassword" &&
                    u.EmailAddress == "theEmailAddress")));
                userRepo.Verify(x => x.CommitChanges());
            }
        }

        static UsersService CreateUsersService(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IEntityRepository<User>> userRepo = null,
            Action<Mock<UsersService>> setup = null) {
            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();

            var usersSvc = new Mock<UsersService>(
                cryptoSvc.Object,
                userRepo.Object);

            usersSvc.CallBase = true;

            if (setup != null)
                setup(usersSvc);

            return usersSvc.Object;
        }
    }
}
