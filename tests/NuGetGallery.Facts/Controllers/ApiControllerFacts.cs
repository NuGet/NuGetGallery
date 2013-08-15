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
using NuGetGallery.Packaging;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    class TestableApiController : ApiController
    {
        public Mock<IEntitiesContext> MockEntitiesContext { get; private set; }
        public Mock<IPackageService> MockPackageService { get; private set; }
        public Mock<IPackageFileService> MockPackageFileService { get; private set; }
        public Mock<IUserService> MockUserService { get; private set; }
        public Mock<INuGetExeDownloaderService> MockNuGetExeDownloaderService { get; private set; }
        public Mock<IContentService> MockContentService { get; private set; }
        public Mock<IStatisticsService> MockStatisticsService { get; private set; }
        public Mock<IIndexingService> MockIndexingService { get; private set; }

        private INupkg PackageFromInputStream { get; set; }

        public TestableApiController(MockBehavior behavior = MockBehavior.Default)
        {
            EntitiesContext = (MockEntitiesContext = new Mock<IEntitiesContext>()).Object;
            PackageService = (MockPackageService = new Mock<IPackageService>(behavior)).Object;
            UserService = (MockUserService = new Mock<IUserService>(behavior)).Object;
            NugetExeDownloaderService = (MockNuGetExeDownloaderService = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict)).Object;
            ContentService = (MockContentService = new Mock<IContentService>()).Object;
            StatisticsService = (MockStatisticsService = new Mock<IStatisticsService>()).Object;
            IndexingService = (MockIndexingService = new Mock<IIndexingService>()).Object;

            MockPackageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
            MockPackageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            PackageFileService = MockPackageFileService.Object;
        }

        internal void SetupPackageFromInputStream(Mock<INupkg> nuGetPackage)
        {
            this.PackageFromInputStream = nuGetPackage.Object;
        }

        protected internal override INupkg ReadPackageFromRequest()
        {
            return this.PackageFromInputStream;
        }
    }

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

        public class TheCreatePackageAction
        {
            [Fact]
            public async Task CreatePackageWillSavePackageFileToFileStorage()
            {
                // Arrange
                var guid = Guid.NewGuid().ToString();
                var fakeUser = new User();
                var userService = new Mock<IUserService>();
                var packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(fakeUser);

                var controller = new TestableApiController();
                controller.MockPackageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(fakeUser);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);

                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut(guid);

                // Assert
                controller.MockPackageFileService.Verify();
            }

            [Fact]
            public async Task WillReturn401IfTheApiKeyDoesNotExist()
            {
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);

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
            public async Task WillReturn401IfTheApiKeyIsNotAValidGuid(string guid)
            {
                var controller = new TestableApiController();

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

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

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

                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                controller.SetupPackageFromInputStream(nuGetPackage);

                var apiKey = Guid.NewGuid();

                controller.CreatePackagePut(apiKey.ToString());

                controller.MockUserService.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillCreateAPackageFromTheNuGetPackage()
            {
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));

                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.CreatePackagePut(Guid.NewGuid().ToString());

                controller.MockPackageService.Verify(x => x.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true));
            }

            [Fact]
            public void WillCreateAPackageWithTheUserMatchingTheApiKey()
            {
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var matchingUser = new User();
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.CreatePackagePut(Guid.NewGuid().ToString());

                controller.MockPackageService.Verify(x => x.CreatePackage(It.IsAny<INupkg>(), matchingUser, true));
            }

            [Fact]
            public void CreatePackageRefreshesNuGetExeIfCommandLinePackageIsUploaded()
            {
                // Arrange
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("NuGet.CommandLine");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var matchingUser = new User();
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(p => p.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true))
                          .Returns(new Package { IsLatestStable = true });
                controller.MockNuGetExeDownloaderService.Setup(s => s.UpdateExecutableAsync(nuGetPackage.Object)).Verifiable();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                controller.MockNuGetExeDownloaderService.Verify();
            }

            [Fact]
            public void CreatePackageDoesNotRefreshNuGetExeIfItIsNotLatestStable()
            {
                // Arrange
                var nuGetPackage = new Mock<INupkg>();
                nuGetPackage.Setup(x => x.Metadata.Id).Returns("NuGet.CommandLine");
                nuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("2.0.0-alpha"));
                var matchingUser = new User();
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(p => p.CreatePackage(nuGetPackage.Object, It.IsAny<User>(), true))
                          .Returns(new Package { IsLatest = true, IsLatestStable = false });
                controller.MockNuGetExeDownloaderService.Setup(s => s.UpdateExecutableAsync(nuGetPackage.Object)).Verifiable();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(matchingUser);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                controller.CreatePackagePut(Guid.NewGuid().ToString());

                // Assert
                controller.MockNuGetExeDownloaderService.Verify(s => s.UpdateExecutableAsync(It.IsAny<INupkg>()), Times.Never());
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
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);

                var result = controller.DeletePackage(guidValue, "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                AssertStatusCodeResult(result, 400, String.Format("The API key '{0}' is invalid.", guidValue));
            }

            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist()
            {
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);

                var result = controller.DeletePackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(403, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "delete"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndSemanticVersionDoesNotExist()
            {
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)null);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());

                var result = controller.DeletePackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(404, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillFindTheUserThatMatchesTheApiKey()
            {
                var owner = new User { Key = 1, ApiKey = Guid.NewGuid() };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                    };

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);

                controller.DeletePackage(owner.ApiKey.ToString(), "theId", "1.0.42");

                controller.MockUserService.Verify(x => x.FindByApiKey(owner.ApiKey));
            }

            [Fact]
            public void WillNotDeleteThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                    };
                var apiKey = Guid.NewGuid();
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.MockPackageService
                    .Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>(), true))
                    .Throws(new InvalidOperationException("Should not have unlisted the package!"));

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
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);
                var apiKey = Guid.NewGuid();

                controller.DeletePackage(apiKey.ToString(), "theId", "1.0.42");

                controller.MockPackageService.Verify(x => x.MarkPackageUnlisted(package, true));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
            }
        }

        public class TheGetPackageAction
        {
            [Fact]
            public async Task GetPackageReturns400ForEvilPackageName()
            {
                var controller = new TestableApiController();
                var result = await controller.GetPackage("../..", "1.0.0.0");
                var badRequestResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(400, badRequestResult.StatusCode);
            }

            [Fact]
            public async Task GetPackageReturns400ForEvilPackageVersion()
            {
                var controller = new TestableApiController();
                var result2 = await controller.GetPackage("Foo", "10../..1.0");
                var badRequestResult2 = (HttpStatusCodeWithBodyResult)result2;
                Assert.Equal(400, badRequestResult2.StatusCode);
            }

            [Fact]
            public async Task GetPackageReturns404IfPackageIsNotFound()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.1", false)).Returns((Package)null).Verifiable();
                controller.MockUserService.Setup(x => x.FindByApiKey(guid)).Returns(new User());

                // Act
                var result = await controller.GetPackage("Baz", "1.0.1");

                // Assert
                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var httpNotFoundResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "Baz", "1.0.1"), httpNotFoundResult.StatusDescription);
                controller.MockPackageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsPackageIfItExists()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var package = new Package();
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.1", false)).Returns(package);
                controller.MockPackageService.Setup(x => x.AddDownloadStatistics(It.IsAny<PackageStatistics>())).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, package))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();
                controller.MockUserService.Setup(x => x.FindByApiKey(guid)).Returns(new User());

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackage("Baz", "1.0.1");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
                controller.MockUserService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsSpecificPackageEvenIfDatabaseIsOffline()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var package = new Package();
                var actionResult = new EmptyResult();

                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.0", false)).Throws(new DataException("Can't find the database")).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, "Baz", "1.0.0"))
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

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackage("Baz", "1.0.0");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsLatestPackageIfNoVersionIsProvided()
            {
                // Arrange
                var guid = Guid.NewGuid();
                var package = new Package();
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "", false)).Returns(package);
                controller.MockPackageService.Setup(x => x.AddDownloadStatistics(It.IsAny<PackageStatistics>())).Verifiable();

                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, package))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();
                controller.MockUserService.Setup(x => x.FindByApiKey(guid)).Returns(new User());

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackage("Baz", "");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
                controller.MockUserService.Verify();
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
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);

                var result = controller.PublishPackage(guidValue, "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                AssertStatusCodeResult(result, 400, String.Format("The API key '{0}' is invalid.", guidValue));
            }

            [Fact]
            public void WillThrowIfTheApiKeyDoesNotExist()
            {
                var controller = new TestableApiController();
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns((User)null);

                var result = controller.PublishPackage(Guid.NewGuid().ToString(), "theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(403, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "publish"), statusCodeResult.StatusDescription);
            }

            [Fact]
            public void WillThrowIfAPackageWithTheIdAndSemanticVersionDoesNotExist()
            {
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)null);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(new User());
                
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
                var apiKey = Guid.NewGuid();

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);

                controller.PublishPackage(apiKey.ToString(), "theId", "1.0.42");

                controller.MockUserService.Verify(x => x.FindByApiKey(apiKey));
            }

            [Fact]
            public void WillNotListThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                    };
                var apiKey = Guid.NewGuid();

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.MockPackageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Throws(new InvalidOperationException("Should not have listed the package!"));
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);

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
                var apiKey = Guid.NewGuid();

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.MockUserService.Setup(x => x.FindByApiKey(It.IsAny<Guid>())).Returns(owner);

                controller.PublishPackage(apiKey.ToString(), "theId", "1.0.42");

                controller.MockPackageService.Verify(x => x.MarkPackageListed(package, It.IsAny<bool>()));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
            }
        }

        public class TheVerifyPackageKeyAction
        {
            [Fact]
            public void VerifyPackageKeyReturns403IfApiKeyIsInvalidGuid()
            {
                // Arrange
                var controller = new TestableApiController();
                
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
                var controller = new TestableApiController();
                controller.MockUserService.Setup(s => s.FindByApiKey(guid)).Returns<User>(null);

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
                var controller = new TestableApiController();
                controller.MockUserService.Setup(s => s.FindByApiKey(guid)).Returns(new User());

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
                var controller = new TestableApiController();
                controller.MockUserService.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                controller.MockPackageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns<Package>(null);

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
                var controller = new TestableApiController();
                controller.MockUserService.Setup(s => s.FindByApiKey(guid)).Returns(new User());
                controller.MockPackageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(
                    new Package { PackageRegistration = new PackageRegistration() });

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
                var user = new User();
                var package = new Package { PackageRegistration = new PackageRegistration() };
                package.PackageRegistration.Owners.Add(user);
                var controller = new TestableApiController();
                controller.MockUserService.Setup(s => s.FindByApiKey(guid)).Returns(user);
                controller.MockPackageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(package);

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

                fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));
                
                var controller = new TestableApiController
                {
                    StatisticsService = new JsonStatisticsService(fakeReportService.Object),
                };

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
                var controller = new TestableApiController();
                controller.MockStatisticsService.Setup(x => x.LoadDownloadPackageVersions()).Returns(Task.FromResult(StatisticsReportResult.Failed));

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

                fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));

                var controller = new TestableApiController
                {
                    StatisticsService = new JsonStatisticsService(fakeReportService.Object),
                };

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