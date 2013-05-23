using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class ApiControllerFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/api/v2/something");
        private static readonly Uri HttpsRequestUrl = new Uri("https://nuget.org/api/v2/something");

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
            Mock<INupkg> packageFromInputStream = null,
            Mock<IIndexingService> indexingService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            userService = userService ?? new Mock<IUserService>();
            indexingService = indexingService ?? new Mock<IIndexingService>();
            if (fileService == null)
            {
                fileService = new Mock<IPackageFileService>(MockBehavior.Strict);
                fileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }
            nugetExeDownloader = nugetExeDownloader ?? new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);

            var controller = new Mock<ApiController>(packageService.Object, fileService.Object, userService.Object, nugetExeDownloader.Object, new Mock<IContentService>().Object, indexingService.Object);
            controller.CallBase = true;
            if (packageFromInputStream != null)
            {
                controller.Setup(x => x.ReadPackageFromRequest()).Returns(packageFromInputStream.Object);
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

                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));

                var controller = CreateController(
                    fileService: packageFileService,
                    userService: userService,
                    packageService: packageService,
                    packageFromInputStream: nuGetPackage);

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
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(version);

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
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage);

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
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage);
                var apiKey = Guid.NewGuid();

                controller.CreatePackagePut(apiKey.ToString());

                userService.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillCreateAPackageFromTheNuGetPackage()
            {
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage);

                controller.CreatePackagePut(Guid.NewGuid().ToString());

                packageService.Verify(x => x.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true));
            }

            [Fact]
            public void WillCreateAPackageWithTheUserMatchingTheApiKey()
            {
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                var matchingUser = new User();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(userService: userService, packageService: packageService, packageFromInputStream: nuGetPackage);

                controller.CreatePackagePut(Guid.NewGuid().ToString());

                packageService.Verify(x => x.CreatePackage(It.IsAny<INupkg>(), matchingUser, true));
            }

            [Fact]
            public void CreatePackageRefreshesNuGetExeIfCommandLinePackageIsUploaded()
            {
                // Arrange
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("NuGet.CommandLine");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true))
                          .Returns(new Package { IsLatestStable = true });
                var userService = new Mock<IUserService>();
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                nugetExeDownloader.Setup(s => s.UpdateExecutableAsync(nuGetPackage.Object)).Verifiable();
                var matchingUser = new User();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(
                    userService: userService, packageService: packageService, nugetExeDownloader: nugetExeDownloader, packageFromInputStream: nuGetPackage);

                // Act
                controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                nugetExeDownloader.Verify();
            }

            [Fact]
            public void CreatePackageDoesNotRefreshNuGetExeIfItIsNotLatestStable()
            {
                // Arrange
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("NuGet.CommandLine");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("2.0.0-alpha"));
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true))
                          .Returns(new Package { IsLatest = true, IsLatestStable = false });
                var userService = new Mock<IUserService>();
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                var matchingUser = new User();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                var controller = CreateController(
                    userService: userService, packageService: packageService, nugetExeDownloader: nugetExeDownloader, packageFromInputStream: nuGetPackage);

                // Act
                controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                nugetExeDownloader.Verify(s => s.UpdateExecutableAsync(It.IsAny<INupkg>()), Times.Never());
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
                var indexingService = new Mock<IIndexingService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService, indexingService: indexingService);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey.ToString(), "theId", "1.0.42");

                packageService.Verify(x => x.MarkPackageUnlisted(package, true));
                indexingService.Verify(i => i.UpdatePackage(package));
            }
        }

        public class TheGetPackageAction
        {
            [Fact]
            public async Task GetPackageReturns400ForEvilPackageName()
            {
                var controller = CreateController();
                var result = await controller.GetPackage("../..", "1.0.0.0");
                var badRequestResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(400, badRequestResult.StatusCode);
            }

            [Fact]
            public async Task GetPackageReturns400ForEvilPackageVersion()
            {
                var controller = CreateController();
                var result2 = await controller.GetPackage("Foo", "10../..1.0");
                var badRequestResult2 = (HttpStatusCodeWithBodyResult)result2;
                Assert.Equal(400, badRequestResult2.StatusCode);
            }

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
                packageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, package))
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
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
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
            public async Task GetPackageReturnsSpecificPackageEvenIfDatabaseIsOffline()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var package = new Package();
                var actionResult = new EmptyResult();
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.0", false)).Throws(new DataException("Can't find the database")).Verifiable();

                var packageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
                packageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, "Baz", "1.0.0"))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controller = CreateController(packageService: packageService, fileService: packageFileService);
                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackage("Baz", "1.0.0");

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
                packageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, package))
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
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
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
                var indexingService = new Mock<IIndexingService>();
                packageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var controller = CreateController(userService: userService, packageService: packageService, indexingService: indexingService);
                var apiKey = Guid.NewGuid();

                controller.PublishPackage(apiKey.ToString(), "theId", "1.0.42");

                packageService.Verify(x => x.MarkPackageListed(package, It.IsAny<bool>()));
                indexingService.Verify(i => i.UpdatePackage(package));
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

            [Fact]
            public async void VerifyRecentPopularityStatsDownloads()
            {
                JArray report = new JArray
                {
                    new JObject
                    {
                        { "PackageId", "A" },
                        { "PackageVersion", "1.0" },
                        { "PackageTitle", "Package A Title" },
                        { "PackageDescription", "Package A Description" },
                        { "PackageIconUrl", "Package A IconUrl" },
                        { "Downloads", 3 }
                    },
                    new JObject
                    {
                        { "PackageId", "A" },
                        { "PackageVersion", "1.1" },
                        { "PackageTitle", "Package A Title" },
                        { "PackageDescription", "Package A Description" },
                        { "PackageIconUrl", "Package A IconUrl" },
                        { "Downloads", 4 }
                    },
                    new JObject
                    {
                        { "PackageId", "B" },
                        { "PackageVersion", "1.0" },
                        { "PackageTitle", "Package B Title" },
                        { "PackageDescription", "Package B Description" },
                        { "PackageIconUrl", "Package B IconUrl" },
                        { "Downloads", 5 }
                    },
                    new JObject
                    {
                        { "PackageId", "B" },
                        { "PackageVersion", "1.1" },
                        { "PackageTitle", "Package B Title" },
                        { "PackageDescription", "Package B Description" },
                        { "PackageIconUrl", "Package B IconUrl" },
                        { "Downloads", 6 }
                    },
                };

                var fakePackageVersionReport = report.ToString();

                var fakeReportService = new Mock<IReportService>();

                fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(fakePackageVersionReport));

                var controller = new ApiController(null, null, null, null, null, null, new JsonStatisticsService(fakeReportService.Object));

                TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

                ActionResult actionResult = await controller.GetStatsDownloads(null);

                ContentResult contentResult = (ContentResult)actionResult;

                JArray result = JArray.Parse(contentResult.Content);

                Assert.True((string)result[3]["Gallery"] == "http://nuget.org/packages/B/1.1", "unexpected content result[3].Gallery");
                Assert.True((int)result[2]["Downloads"] == 5, "unexpected content result[2].Downloads");
                Assert.True((string)result[1]["PackageDescription"] == "Package A Description", "unexpected content result[1].PackageDescription");
            }

            [Fact]
            public async void VerifyStatsDownloadsReturnsNotFoundWhenStatsNotAvailable()
            {
                var fakeStatisticsService = new Mock<IStatisticsService>();

                fakeStatisticsService.Setup(x => x.LoadDownloadPackageVersions()).Returns(Task.FromResult(false));

                var controller = new ApiController(null, null, null, null, null, null, fakeStatisticsService.Object);

                TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

                ActionResult actionResult = await controller.GetStatsDownloads(null);

                HttpStatusCodeResult httpStatusResult = (HttpStatusCodeResult)actionResult;

                Assert.True(httpStatusResult.StatusCode == (int)HttpStatusCode.NotFound, "unexpected StatusCode");
            }

            [Fact]
            public async void VerifyRecentPopularityStatsDownloadsCount()
            {
                JArray report = new JArray
                {
                    new JObject { { "PackageId", "A" }, { "PackageVersion", "1.0" }, { "Downloads", 3 } },
                    new JObject { { "PackageId", "A" }, { "PackageVersion", "1.1" }, { "Downloads", 4 } },
                    new JObject { { "PackageId", "B" }, { "PackageVersion", "1.0" }, { "Downloads", 5 } },
                    new JObject { { "PackageId", "B" }, { "PackageVersion", "1.1" }, { "Downloads", 6 } },
                    new JObject { { "PackageId", "C" }, { "PackageVersion", "1.0" }, { "Downloads", 7 } },
                    new JObject { { "PackageId", "C" }, { "PackageVersion", "1.1" }, { "Downloads", 8 } },
                };

                var fakePackageVersionReport = report.ToString();

                var fakeReportService = new Mock<IReportService>();

                fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(fakePackageVersionReport));

                var controller = new ApiController(null, null, null, null, null, null, new JsonStatisticsService(fakeReportService.Object));

                TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

                T4MVCHelpers.ProcessVirtualPath = s => s;

                ActionResult actionResult = await controller.GetStatsDownloads(3);

                ContentResult contentResult = (ContentResult)actionResult;

                JArray result = JArray.Parse(contentResult.Content);

                Assert.True(result.Count == 3, "unexpected content");
            }
        }
    }
}