using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet;
using Xunit;

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
                var result = controller.CreatePackagePut(Guid.NewGuid());

                // Assert
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "push"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillReturnConflictIfAPackageWithTheIdAndSemanticVersionAlreadyExists()
            {
                var version = new SemanticVersion("1.0.42");
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(version);

                var user = new User();

                var packageRegistration = new PackageRegistration { 
                    Packages = new List<Package> { new Package { Version = version.ToString() } }, 
                    Owners =  new List<User> { user }
                };

                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(user);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc, packageFromInputStream: nuGetPackage.Object);

                // Act
                var result = controller.CreatePackagePut(Guid.NewGuid());

                // Assert
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
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

                controller.CreatePackagePut(apiKey);

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

                controller.CreatePackagePut(Guid.NewGuid());

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

                controller.CreatePackagePut(Guid.NewGuid());

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

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(403, statusCodeResult.StatusCode);
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

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(404, statusCodeResult.StatusCode);
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

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
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
            public void PublishPackageNoOps()
            {
                var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
                var userSvc = new Mock<IUserService>(MockBehavior.Strict);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);
                var apiKey = Guid.NewGuid();

                var result = controller.PublishPackage(apiKey, "theId", "1.0.42");

                Assert.IsType<EmptyResult>(result);
            }
        }

        public class TheVerifyPackageKeyAction
        {
            [Fact]
            public void VerifyPackageKeyReturns403IfUserDoesNotExist()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userSvc = new Mock<IUserService>(MockBehavior.Strict);
                userSvc.Setup(s => s.FindByApiKey(guid)).Returns<User>(null);
                var controller = CreateController(userSvc: userSvc);

                // Act
                var result = controller.VerifyPackageKey(guid, "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 403, "The specified API key does not provide the authority to push packages.");
            }

            [Fact]
            public void VerifyPackageKeyReturnsEmptyResultIfApiKeyExistsAndIdAndVersionAreEmpty()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userSvc = new Mock<IUserService>(MockBehavior.Strict);
                userSvc.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                var controller = CreateController(userSvc: userSvc);

                // Act
                var result = controller.VerifyPackageKey(guid, null, null);

                // Assert
                Assert.IsType<EmptyResult>(result);
            }

            [Fact]
            public void VerifyPackageKeyReturns404IfPackageDoesNotExist()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userSvc = new Mock<IUserService>(MockBehavior.Strict);
                userSvc.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
                packageSvc.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns<Package>(null);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);

                // Act
                var result = controller.VerifyPackageKey(guid, "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 404, "A package with id 'foo' and version '1.0.0' does not exist.");
            }

            [Fact]
            public void VerifyPackageKeyReturns403IfUserIsNotAnOwner()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userSvc = new Mock<IUserService>(MockBehavior.Strict);
                userSvc.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
                packageSvc.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(new Package { PackageRegistration = new PackageRegistration() });
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);

                // Act
                var result = controller.VerifyPackageKey(guid, "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 403, "The specified API key does not provide the authority to push packages.");
            }

            [Fact]
            public void VerifyPackageKeyReturns200IfUserIsAnOwner()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userSvc = new Mock<IUserService>(MockBehavior.Strict);
                var user = new User();
                var package = new Package { PackageRegistration = new PackageRegistration() };
                package.PackageRegistration.Owners.Add(user);
                userSvc.Setup(s => s.FindByApiKey(guid)).Returns(user);
                var packageSvc = new Mock<IPackageService>(MockBehavior.Strict);
                packageSvc.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(package);
                var controller = CreateController(userSvc: userSvc, packageSvc: packageSvc);

                // Act
                var result = controller.VerifyPackageKey(guid, "foo", "1.0.0");

                Assert.IsType<EmptyResult>(result);
            }
        }

        public class TheGetPackageAction
        {
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
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var httpNotFoundResult = (HttpStatusCodeWithBodyResult)result;
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

            [Fact]
            public void GetPackageReturnsRedirectResultWhenExternalPackageUrlIsNotNull()
            {
                var package = new Package() { ExternalPackageUrl = "http://theUrl" };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(x => x.FindPackageByIdAndVersion("thePackage", "42.1066", false)).Returns(package);
                var httpRequest = new Mock<HttpRequestBase>();
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                var httpContext = new Mock<HttpContextBase>();
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);
                var controller = CreateController(packageSvc: packageSvc);
                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                var result = controller.GetPackage("thePackage", "42.1066") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("http://theUrl", result.Url);
            }
        }

        private static void AssertStatusCodeResult(ActionResult result, int statusCode, string statusDesc)
        {
            Assert.IsType<HttpStatusCodeWithBodyResult>(result);
            var httpStatus = (HttpStatusCodeWithBodyResult)result;
            Assert.Equal(statusCode, httpStatus.StatusCode);
            Assert.Equal(statusDesc, httpStatus.StatusDescription);
        }

        private static ApiController CreateController(
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