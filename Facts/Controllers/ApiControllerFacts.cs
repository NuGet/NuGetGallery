﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class ApiControllerFacts
    {
        private static void AssertStatusCodeResult(ActionResult result, int statusCode, string statusDesc)
        {
            Assert.IsType<HttpStatusCodeWithBodyResult>(result);
            var httpStatus = (HttpStatusCodeWithBodyResult)result;
            Assert.Equal(statusCode, httpStatus.StatusCode);
            Assert.Equal(statusDesc, httpStatus.StatusDescription);
        }

        private static ApiController CreateController(
            Mock<IPackageService> packageService = null,
            Mock<IPackageFileService> fileService = null,
            Mock<IUserService> userService = null,
            Mock<INuGetExeDownloaderService> nugetExeDownloader = null,
            IPackage packageFromInputStream = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            userService = userService ?? new Mock<IUserService>();
            if (fileService == null)
            {
                fileService = new Mock<IPackageFileService>(MockBehavior.Strict);
                fileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }
            nugetExeDownloader = nugetExeDownloader ?? new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);

            var controller = new Mock<ApiController>(packageService.Object, fileService.Object, userService.Object, nugetExeDownloader.Object);
            controller.CallBase = true;
            if (packageFromInputStream != null)
            {
                controller.Setup(x => x.ReadPackageFromRequest()).Returns(packageFromInputStream);
            }
            return controller.Object;
        }

        public class TheCreatePackageAction
        {
            [Fact]
            public async Task CreatePackageWillSavePackageFileToFileStorage()
            {
                // Arrange
                var guid = Guid.NewGuid().ToString();

                var fakeUser = new User();
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(fakeUser);

                var packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(fakeUser);

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);

                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();

                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));

                var controller = CreateController(
                    fileService: packageFileService,
                    userService: userService,
                    packageService: packageService,
                    packageFromInputStream: nuGetPackage.Object);

                // Act
                await controller.CreatePackagePut(guid);

                // Assert
                packageFileService.Verify();
            }

            [Fact]
            public async Task WillReturnAn401IfTheApiKeyDoesNotExist()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userService: userService);

                // Act
                var result = await controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "push"), statusCodeResult.StatusDescription);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("this-is-bad-guid")]
            public async Task WillReturnAn401IfTheApiKeyIsNotAValidGuid(string guid)
            {
                var controller = CreateController();

                // Act
                var result = await controller.CreatePackagePut(guid);

                // Assert
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.InvalidApiKey, guid), statusCodeResult.StatusDescription);
            }

            [Fact]
            public async Task WillReturnConflictIfAPackageWithTheIdAndSemanticVersionAlreadyExists()
            {
                var version = new SemanticVersion("1.0.42");
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(version);

                var user = new User();

                var packageRegistration = new PackageRegistration
                    {
                        Packages = new List<Package> { new Package { Version = version.ToString() } },
                        Owners = new List<User> { user }
                    };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(user);
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage.Object);

                // Act
                var result = await controller.CreatePackagePut(Guid.NewGuid().ToString());

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
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage.Object);
                var apiKey = Guid.NewGuid();

                controller.CreatePackagePut(apiKey.ToString());

                userService.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillCreateAPackageFromTheNuGetPackage()
            {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage.Object);

                controller.CreatePackagePut(Guid.NewGuid().ToString());

                packageService.Verify(x => x.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true));
            }

            [Fact]
            public void WillCreateAPackageWithTheUserMatchingTheApiKey()
            {
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                var matchingUser = new User();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage.Object);

                controller.CreatePackagePut(Guid.NewGuid().ToString());

                packageService.Verify(x => x.CreatePackage(It.IsAny<IPackage>(), matchingUser, true));
            }

            [Fact]
            public void CreatePackageRefreshesNuGetExeIfCommandLinePackageIsUploaded()
            {
                // Arrange
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("NuGet.CommandLine");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true))
                          .Returns(new Package { IsLatestStable = true });
                var userService = new Mock<IUserService>();
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                nugetExeDownloader.Setup(s => s.UpdateExecutableAsync(nuGetPackage.Object)).Verifiable();
                var matchingUser = new User();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(
                    userService: userService, packageService: packageService, nugetExeDownloader: nugetExeDownloader, packageFromInputStream: nuGetPackage.Object);

                // Act
                controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                nugetExeDownloader.Verify();
            }

            [Fact]
            public void CreatePackageDoesNotRefreshNuGetExeIfItIsNotLatestStable()
            {
                // Arrange
                var nuGetPackage = new Mock<IPackage>();
                nuGetPackage.Setup(x => x.Id).Returns("NuGet.CommandLine");
                nuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("2.0.0-alpha"));
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true))
                          .Returns(new Package { IsLatest = true, IsLatestStable = false });
                var userService = new Mock<IUserService>();
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                var matchingUser = new User();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(
                    userService: userService, packageService: packageService, nugetExeDownloader: nugetExeDownloader, packageFromInputStream: nuGetPackage.Object);

                // Act
                controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                nugetExeDownloader.Verify(s => s.UpdateExecutableAsync(It.IsAny<IPackage>()), Times.Never());
            }
        }

        public class TheDeletePackageAction
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("this-is-bad-guid")]
            public void WillThrowIfTheApiKeyIsAnInvalidGuid(string guidValue)
            {
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userService: userService);

                var result = controller.DeletePackage(guidValue, "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                AssertStatusCodeResult(result, 400, String.Format("The API key '{0}' is invalid.", guidValue));
            }

            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userService: userService);

                var result = controller.DeletePackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(403, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "delete"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndSemanticVersionDoesNotExist()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)null);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService);

                var result = controller.DeletePackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

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
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey.ToString(), "theId", "1.0.42");

                userService.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillNotDeleteThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                    };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>(), true)).Throws(
                    new InvalidOperationException("Should not have unlisted the package!"));
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService);
                var apiKey = Guid.NewGuid();

                var result = controller.DeletePackage(apiKey.ToString(), "theId", "1.0.42");

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
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey.ToString(), "theId", "1.0.42");

                packageService.Verify(x => x.MarkPackageUnlisted(package, true));
            }
        }

        public class TheGetPackageAction
        {
            [Fact]
            public async Task GetPackageReturns404IfPackageIsNotFound()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.1", false)).Returns((Package)null).Verifiable();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(x => x.FindByApiKey(guid)).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService);

                // Act
                var result = await controller.GetPackage("Baz", "1.0.1");

                // Assert
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var httpNotFoundResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "Baz", "1.0.1"), httpNotFoundResult.StatusDescription);
                packageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsPackageIfItExists()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var package = new Package();
                var actionResult = new EmptyResult();
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.1", false)).Returns(package);
                packageService.Setup(x => x.AddDownloadStatistics(package, "Foo", "Qux", "Install")).Verifiable();

                var packageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
                packageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(package))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(x => x.FindByApiKey(guid)).Returns(new User());

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controller = CreateController(userService: userService, packageService: packageService, fileService: packageFileService);
                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackage("Baz", "1.0.1");

                // Assert
                Assert.Same(actionResult, result);
                packageFileService.Verify();
                packageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsLatestPackageIfNoVersionIsProvided()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var package = new Package();
                var actionResult = new EmptyResult();
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "", false)).Returns(package);
                packageService.Setup(x => x.AddDownloadStatistics(package, "Foo", "Qux", "Install")).Verifiable();

                var packageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
                packageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(package))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(x => x.FindByApiKey(guid)).Returns(new User());

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controller = CreateController(userService: userService, packageService: packageService, fileService: packageFileService);
                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackage("Baz", "");

                // Assert
                Assert.Same(actionResult, result);
                packageFileService.Verify();
                packageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsRedirectResultWhenExternalPackageUrlIsNotNull()
            {
                var package = new Package { ExternalPackageUrl = "http://theUrl" };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion("thePackage", "42.1066", false)).Returns(package);
                var httpRequest = new Mock<HttpRequestBase>();
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);
                var controller = CreateController(packageService);
                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                var result = await controller.GetPackage("thePackage", "42.1066") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("http://theUrl", result.Url);
            }
        }

        public class ThePublishPackageAction
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("this-is-bad-guid")]
            public void WillThrowIfTheApiKeyIsAnInvalidGuid(string guidValue)
            {
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userService: userService);

                var result = controller.PublishPackage(guidValue, "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                AssertStatusCodeResult(result, 400, String.Format("The API key '{0}' is invalid.", guidValue));
            }

            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);
                var controller = CreateController(userService: userService);

                var result = controller.PublishPackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(403, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "publish"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndSemanticVersionDoesNotExist()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)null);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService);

                var result = controller.PublishPackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

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
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey.ToString(), "theId", "1.0.42");

                userService.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillNotListThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                    };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>(), It.IsAny<bool>())).Throws(
                    new InvalidOperationException("Should not have listed the package!"));
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService);
                var apiKey = Guid.NewGuid();

                var result = controller.PublishPackage(apiKey.ToString(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "publish"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillListThePackageIfApiKeyBelongsToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                    };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey.ToString(), "theId", "1.0.42");

                packageService.Verify(x => x.MarkPackageListed(package, It.IsAny<bool>()));
            }
        }

        public class TheVerifyPackageKeyAction
        {
            [Fact]
            public void VerifyPackageKeyReturns403IfApiKeyIsInvalidGuid()
            {
                // Arrange
                var controller = CreateController();

                // Act
                var result = controller.VerifyPackageKey("bad-guid", "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 400, "The API key 'bad-guid' is invalid.");
            }

            [Fact]
            public void VerifyPackageKeyReturns403IfUserDoesNotExist()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(s => s.FindByApiKey(guid)).Returns<User>(null);
                var controller = CreateController(userService: userService);

                // Act
                var result = controller.VerifyPackageKey(guid.ToString(), "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 403, "The specified API key does not provide the authority to push packages.");
            }

            [Fact]
            public void VerifyPackageKeyReturnsEmptyResultIfApiKeyExistsAndIdAndVersionAreEmpty()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                var controller = CreateController(userService: userService);

                // Act
                var result = controller.VerifyPackageKey(guid.ToString(), null, null);

                // Assert
                Assert.IsType<EmptyResult>(result);
            }

            [Fact]
            public void VerifyPackageKeyReturns404IfPackageDoesNotExist()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns<Package>(null);
                var controller = CreateController(userService: userService, packageService: packageService);

                // Act
                var result = controller.VerifyPackageKey(guid.ToString(), "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 404, "A package with id 'foo' and version '1.0.0' does not exist.");
            }

            [Fact]
            public void VerifyPackageKeyReturns403IfUserIsNotAnOwner()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                userService.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(
                    new Package { PackageRegistration = new PackageRegistration() });
                var controller = CreateController(userService: userService, packageService: packageService);

                // Act
                var result = controller.VerifyPackageKey(guid.ToString(), "foo", "1.0.0");

                // Assert
                AssertStatusCodeResult(result, 403, "The specified API key does not provide the authority to push packages.");
            }

            [Fact]
            public void VerifyPackageKeyReturns200IfUserIsAnOwner()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                var user = new User();
                var package = new Package { PackageRegistration = new PackageRegistration() };
                package.PackageRegistration.Owners.Add(user);
                userService.Setup(s => s.FindByApiKey(guid)).Returns(user);
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(package);
                var controller = CreateController(userService: userService, packageService: packageService);

                // Act
                var result = controller.VerifyPackageKey(guid.ToString(), "foo", "1.0.0");

                Assert.IsType<EmptyResult>(result);
            }
        }
    }
}