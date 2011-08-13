using System;
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
