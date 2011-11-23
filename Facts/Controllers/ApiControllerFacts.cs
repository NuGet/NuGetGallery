using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet;
using Xunit;
using System.Web.Routing;

namespace NuGetGallery
{
    public class ApiControllerFacts
    {
        public class TheCreatePackageAction
        {
            [Fact]
            public void WillReturnAn401IfTheApiKeyDoesNotExist()
            {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                // Act
                var result = controller.CreatePackage(Guid.NewGuid());

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var statusCodeResult = (HttpStatusCodeResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "push"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillReturnConflictIfAPackageWithTheIdAndSemanticVersionAlreadyExists()
            {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                // Act
                var result = controller.CreatePackage(Guid.NewGuid());

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var statusCodeResult = (HttpStatusCodeResult)result;
                Assert.Equal(409, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "1.0.42"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey()
            {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);
                var apiKey = Guid.NewGuid();

                controller.CreatePackage(apiKey);

                userSvc.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillCreateAPackageFromTheNuGetPackage()
            {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                controller.CreatePackage(Guid.NewGuid());

                packageSvc.Verify(x => x.CreatePackage(nuGetPackage.Object, It.IsAny<User>()));
            }

            [Fact]
            public void WillCreateAPackageWithTheUserMatchingTheApiKey()
            {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageSvc = new Mock<IPackageService>();
                var userSvc = new Mock<IUserService>();
                var matchingUser = new User();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                controller.CreatePackage(Guid.NewGuid());

                packageSvc.Verify(x => x.CreatePackage(It.IsAny<IPackage>(), matchingUser));
            }
        }

        public class TheDeletePackageAction
        {
            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist()
            {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                var result = controller.DeletePackage(Guid.NewGuid(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeResult>(result);
                var statusCodeResult = (HttpStatusCodeResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "delete"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndSemanticVersionDoesNotExist()
            {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)null);
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);

                var result = controller.DeletePackage(Guid.NewGuid(), "theId", "1.0.42");

                Assert.IsType<HttpNotFoundResult>(result);
                var statusCodeResult = (HttpNotFoundResult)result;
                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey, "theId", "1.0.42");

                userSvc.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillNotDeleteThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                packageSvc.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>())).Throws(new InvalidOperationException("Should not have unlisted the package!"));
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                var result = controller.DeletePackage(apiKey, "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeResult>(result);
                var statusCodeResult = (HttpStatusCodeResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "delete"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillUnlistThePackageIfApiKeyBelongsToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey, "theId", "1.0.42");

                packageSvc.Verify(x => x.MarkPackageUnlisted(package));
            }
        }

        public class ThePublishPackageAction
        {
            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist()
            {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userSvc: userSvc);


                var result = controller.PublishPackage(Guid.NewGuid(), "theId", "1.0.42");

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var httpNotFoundResult = (HttpStatusCodeResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "publish"), httpNotFoundResult.StatusDescription);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndSemanticVersionDoesNotExist()
            {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                var packageRepo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepo.Setup(r => r.GetAll()).Returns(new[] { new Package { PackageRegistration = new PackageRegistration { Id = "not-the-id" }, Version = "1.1" } }.AsQueryable())
                                                  .Verifiable(); 
                var packageService = new PackageService(new Mock<ICryptographyService>(MockBehavior.Strict).Object, packageRegistrationRepo.Object, packageRepo.Object, 
                    new Mock<IEntityRepository<PackageStatistics>>(MockBehavior.Strict).Object, 
                    new Mock<IPackageFileService>(MockBehavior.Strict).Object, 
                    new Mock<IEntityRepository<PackageOwnerRequest>>(MockBehavior.Strict).Object);

                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = new Mock<ApiController>(packageService, new Mock<IPackageFileService>().Object, userSvc.Object) { CallBase = true };
                var ex = Assert.Throws<EntityException>(() => controller.Object.PublishPackage(Guid.NewGuid(), "theId", "1.0.42"));

                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
                packageRepo.Verify();
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey()
            {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey, "theId", "1.0.42");

                userSvc.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillPublishThePackage()
            {
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(new Package());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey, "theId", "1.0.42");

                packageSvc.Verify(x => x.PublishPackage("theId", "1.0.42"));
            }
        }

        [Fact]
        public void GetPackageReturns404IfPackageIsNotFound()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
            packageSvc.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.1", false)).Returns((Package)null).Verifiable();
            var userSvc = new Mock<IUserService>(MockBehavior.Strict);
            userSvc.Setup(x => x.FindByApiKey(guid)).Returns(new User());
            var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
            
            // Act
            var result = controller.GetPackage("Baz", "1.0.1");

            // Assert
            Assert.IsType<HttpNotFoundResult>(result);
            var httpNotFoundResult = (HttpNotFoundResult)result;
            Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "Baz", "1.0.1"), httpNotFoundResult.StatusDescription);
            packageSvc.Verify();
        }

        [Fact]
        public void GetPackageReturnsPackageIfItExists()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var package = new Package();
            var actionResult = new EmptyResult();
            var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
            packageSvc.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.1", false)).Returns(package);
            packageSvc.Setup(x => x.AddDownloadStatistics(package, "Foo", "Qux")).Verifiable();

            var packageFileSvc = new Mock<IPackageFileService>(MockBehavior.Strict);
            packageFileSvc.Setup(s => s.CreateDownloadPackageActionResult(package)).Returns(actionResult).Verifiable();
            var userSvc = new Mock<IUserService>(MockBehavior.Strict);
            userSvc.Setup(x => x.FindByApiKey(guid)).Returns(new User());

            var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
            httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);
            
            var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, fileService: packageFileSvc);
            var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
            controller.ControllerContext = controllerContext;

            // Act
            var result = controller.GetPackage("Baz", "1.0.1");

            // Assert
            Assert.Same(actionResult, result);
            packageFileSvc.Verify();
            packageSvc.Verify();
        }

        [Fact]
        public void GetPackageReturnsLatestPackageIfNoVersionIsProvided()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var package = new Package();
            var actionResult = new EmptyResult();
            var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
            packageSvc.Setup(x => x.FindPackageByIdAndVersion("Baz", "", false)).Returns(package);
            packageSvc.Setup(x => x.AddDownloadStatistics(package, "Foo", "Qux")).Verifiable();

            var packageFileSvc = new Mock<IPackageFileService>(MockBehavior.Strict);
            packageFileSvc.Setup(s => s.CreateDownloadPackageActionResult(package)).Returns(actionResult).Verifiable();
            var userSvc = new Mock<IUserService>(MockBehavior.Strict);
            userSvc.Setup(x => x.FindByApiKey(guid)).Returns(new User());

            var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
            httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

            var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, fileService: packageFileSvc);
            var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
            controller.ControllerContext = controllerContext;

            // Act
            var result = controller.GetPackage("Baz", "");

            // Assert
            Assert.Same(actionResult, result);
            packageFileSvc.Verify();
            packageSvc.Verify();
        }

        static ApiController CreateController(
            Mock<IPackageService> packageSvc = null,
            Mock<IPackageFileService> fileService = null,
            Mock<IUserService> userSvc = null,
            IPackage packageFromInputStream = null)
        {
            packageSvc = packageSvc ?? new Mock<IPackageService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            fileService = fileService ?? new Mock<IPackageFileService>(MockBehavior.Strict);
            var controller = new Mock<ApiController>(packageSvc.Object, fileService.Object, userSvc.Object);
            controller.CallBase = true;
            if (packageFromInputStream != null)
                controller.Setup(x => x.ReadPackageFromRequest()).Returns(packageFromInputStream);
            return controller.Object;
        }
    }
}