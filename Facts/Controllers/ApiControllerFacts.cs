using System;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery {
    public class ApiControllerFacts {
        public class TheCreatePackageAction {
            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist() {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                var ex = Assert.Throws<EntityException>(() => controller.CreatePackage(Guid.NewGuid()));

                Assert.Equal(string.Format(Strings.ApiKeyNotAuthorized, "push"), ex.Message);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndVersionAlreadyExists() {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new Version("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                var ex = Assert.Throws<EntityException>(() => controller.CreatePackage(Guid.NewGuid()));

                Assert.Equal(string.Format(Strings.PackageExistsAndCannotBeModified, "theId", "1.0.42"), ex.Message);
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey() {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new Version("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);
                var apiKey = Guid.NewGuid();

                controller.CreatePackage(apiKey);

                userSvc.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillCreateAPackageFromTheNuGetPackage() {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new Version("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                controller.CreatePackage(Guid.NewGuid());

                packageSvc.Verify(x => x.CreatePackage(nuGetPackage.Object, It.IsAny<User>()));
            }

            [Fact]
            public void WillCreateAPackageWithTheUserMatchingTheApiKey() {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new Version("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                var userSvc = new Mock<IUserService>();
                var matchingUser = new User();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                controller.CreatePackage(Guid.NewGuid());

                packageSvc.Verify(x => x.CreatePackage(It.IsAny<IPackage>(), matchingUser));
            }
        }

        public class TheDeletePackageAction {
            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist() {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                var ex = Assert.Throws<EntityException>(() => controller.DeletePackage(Guid.NewGuid(), "theId", "1.0.42"));

                Assert.Equal(string.Format(Strings.ApiKeyNotAuthorized, "delete"), ex.Message);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndVersionDoesNotExist() {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)null);
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);

                var ex = Assert.Throws<EntityException>(() => controller.DeletePackage(Guid.NewGuid(), "theId", "1.0.42"));

                Assert.Equal(string.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey() {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey, "theId", "1.0.42");

                userSvc.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillDeleteThePackage() {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey, "theId", "1.0.42");

                packageSvc.Verify(x => x.DeletePackage("theId", "1.0.42"));
            }
        }

        public class ThePublishPackageAction {
            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist() {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                var ex = Assert.Throws<EntityException>(() => controller.PublishPackage(Guid.NewGuid(), "theId", "1.0.42"));

                Assert.Equal(string.Format(Strings.ApiKeyNotAuthorized, "publish"), ex.Message);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndVersionDoesNotExist() {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)null);
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);

                var ex = Assert.Throws<EntityException>(() => controller.PublishPackage(Guid.NewGuid(), "theId", "1.0.42"));

                Assert.Equal(string.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey() {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey, "theId", "1.0.42");

                userSvc.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillPublishThePackage() {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey, "theId", "1.0.42");

                packageSvc.Verify(x => x.PublishPackage("theId", "1.0.42"));
            }
        }

        static ApiController CreateController(
            Mock<IPackageService> packageSvc = null,
            Mock<IUserService> userSvc = null,
            IPackage packageFromInputStream = null) {
            packageSvc = packageSvc ?? new Mock<IPackageService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            var controller = new Mock<ApiController>(packageSvc.Object, userSvc.Object);
            controller.CallBase = true;
            if (packageFromInputStream != null)
                controller.Setup(x => x.ReadPackageFromRequest()).Returns(packageFromInputStream);
            return controller.Object;
        }
    }
}