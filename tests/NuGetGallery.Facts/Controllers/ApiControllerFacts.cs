// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using Xunit;

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
        public Mock<IAutomaticallyCuratePackageCommand> MockAutoCuratePackage { get; private set; }
        public Mock<IMessageService> MockMessageService { get; private set; }
        public Mock<IGalleryConfigurationService> MockConfigurationService { get; private set; }

        private Stream PackageFromInputStream { get; set; }

        public TestableApiController(MockBehavior behavior = MockBehavior.Default)
        {
            OwinContext = Fakes.CreateOwinContext();
            EntitiesContext = (MockEntitiesContext = new Mock<IEntitiesContext>()).Object;
            PackageService = (MockPackageService = new Mock<IPackageService>(behavior)).Object;
            UserService = (MockUserService = new Mock<IUserService>(behavior)).Object;
            NugetExeDownloaderService = (MockNuGetExeDownloaderService = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict)).Object;
            ContentService = (MockContentService = new Mock<IContentService>()).Object;
            StatisticsService = (MockStatisticsService = new Mock<IStatisticsService>()).Object;
            IndexingService = (MockIndexingService = new Mock<IIndexingService>()).Object;
            AutoCuratePackage = (MockAutoCuratePackage = new Mock<IAutomaticallyCuratePackageCommand>()).Object;

            MockPackageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
            MockPackageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                .Returns(Task.CompletedTask);
            PackageFileService = MockPackageFileService.Object;

            MessageService = (MockMessageService = new Mock<IMessageService>()).Object;

            MockConfigurationService = new Mock<IGalleryConfigurationService>();
            MockConfigurationService.SetupGet(s => s.Features.TrackPackageDownloadCountInLocalDatabase)
                .Returns(false);
            ConfigurationService = MockConfigurationService.Object;

            AuditingService = new TestAuditingService();

            TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), this);
        }

        internal void SetupPackageFromInputStream(Stream packageStream)
        {
            PackageFromInputStream = packageStream;
        }

        protected internal override Stream ReadPackageFromRequest()
        {
            return PackageFromInputStream;
        }
    }

    public class ApiControllerFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/api/v2/something");
        private static readonly Uri HttpsRequestUrl = new Uri("https://nuget.org/api/v2/something");

        public class TheCreatePackageAction
        {
            [Fact]
            public async Task CreatePackageWillSavePackageFileToFileStorage()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                packageRegistration.Owners.Add(user);
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockPackageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask).Verifiable();
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);
                controller.MockPackageService.Setup(p => p.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), false))
                    .Returns(Task.FromResult(package));

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                controller.MockPackageFileService.Verify();
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                packageRegistration.Owners.Add(user);
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(p => p.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), false))
                    .Returns(Task.FromResult(package));

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                Assert.True(controller.AuditingService.WroteRecord<PackageAuditRecord>(ar =>
                    ar.Action == AuditedPackageAction.Create
                    && ar.Id == package.PackageRegistration.Id
                    && ar.Version == package.Version));
            }

            [Fact]
            public async Task CreatePackageWillSendPackageAddedNotice()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                packageRegistration.Owners.Add(user);
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockMessageService.Setup(p => p.SendPackageAddedNotice(package, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Verifiable();
                controller.MockPackageService.Setup(p => p.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), false))
                    .Returns(Task.FromResult(package));

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                controller.MockMessageService.Verify();
            }

            [Fact]
            public async Task CreatePackageWillReturn400IfPackageIsInvalid()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(user);

                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockPackageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask).Verifiable();
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                byte[] data = new byte[100];
                controller.SetupPackageFromInputStream(new MemoryStream(data));

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }

            [Fact]
            public async Task WillWriteAnAuditRecordIfUserIsNotPackageOwner()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                Assert.True(controller.AuditingService.WroteRecord<FailedAuthenticatedOperationAuditRecord>(ar =>
                    ar.Action == AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner
                    && ar.AttemptedPackage.Id == package.PackageRegistration.Id
                    && ar.AttemptedPackage.Version == package.Version));
            }

            [Fact]
            public async Task WillReturnConflictIfAPackageWithTheIdAndSameNormalizedVersionAlreadyExists()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.042");

                var user = new User() { EmailAddress = "confirmed@email.com" };

                var packageRegistration = new PackageRegistration
                {
                    Packages = new List<Package> { new Package { Version = "01.00.42", NormalizedVersion = "1.0.42" } },
                    Owners = new List<User> { user }
                };

                var controller = new TestableApiController();
                controller.SetCurrentUser(new User());
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById("theId")).Returns(packageRegistration);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "1.0.42"));
            }

            [Fact]
            public async Task WillReturnConflictIfAPackageWithTheIdExistsBelongingToAnotherUser()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var packageId = "theId";
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = packageId;
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");
                controller.SetCurrentUser(new User());
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    String.Format(Strings.PackageIdNotAvailable, packageId));
            }

            [Fact]
            public void WillCreateAPackageFromTheNuGetPackage()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.CreatePackagePut();

                controller.MockPackageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), false));
                controller.MockEntitiesContext.VerifyCommitted();
            }

            [Fact]
            public void WillCurateThePackage()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.CreatePackagePut();

                controller.MockAutoCuratePackage.Verify(x => x.ExecuteAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), false));
                controller.MockEntitiesContext.VerifyCommitted();
            }

            [Fact]
            public void WillCurateThePackageViaApi()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.CreatePackagePost();

                controller.MockAutoCuratePackage.Verify(x => x.ExecuteAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), false));
                controller.MockEntitiesContext.VerifyCommitted();
            }

            [Fact]
            public void WillCreateAPackageWithTheUserMatchingTheApiKey()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.CreatePackagePut();

                controller.MockPackageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), user, false));
                controller.MockEntitiesContext.VerifyCommitted();
            }
        }

        public class TheDeletePackageAction
        {
            [Fact]
            public async Task WillThrowIfAPackageWithTheIdAndNuGetVersionDoesNotExist()
            {
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("theId", "1.0.42", true)).Returns((Package)null);
                controller.SetCurrentUser(new User());

                var result = await controller.DeletePackage("theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(404, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), statusCodeResult.StatusDescription);
                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(It.IsAny<Package>(), true), Times.Never());
            }

            [Fact]
            public async Task WillNotDeleteThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var notOwner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                    };

                var controller = new TestableApiController();
                controller.SetCurrentUser(notOwner);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("theId", "1.0.42", true)).Returns(package);

                var result = await controller.DeletePackage("theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(String.Format(Strings.ApiKeyNotAuthorized, "delete"), statusCodeResult.StatusDescription);

                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true), Times.Never());
            }

            [Fact]
            public async Task WillUnlistThePackageIfApiKeyBelongsToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                    };
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.SetCurrentUser(owner);

                ResultAssert.IsEmpty(await controller.DeletePackage("theId", "1.0.42"));

                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true));
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
                const string packageId = "Baz";
                const string packageVersion = "1.0.1";
                var actionResult = new RedirectResult("http://foo");

                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(packageId, packageVersion, false)).Returns((Package)null).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(It.IsAny<Uri>(), packageId, packageVersion))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();

                // Act
                var result = await controller.GetPackage(packageId, packageVersion);

                // Assert
                Assert.IsType<RedirectResult>(result); // all we want to check is that we're redirecting to storage
                //var httpNotFoundResult = (RedirectResult)result;
                //Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, packageId, packageVersion), httpNotFoundResult.StatusDescription);
                //controller.MockPackageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsPackageIfItExists()
            {
                // Arrange
                const string packageId = "Baz";
                var package = new Package() { Version = "1.0.01", NormalizedVersion = "1.0.1" };
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(MockBehavior.Strict);
                // controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(PackageId, "1.0.1", false)).Returns(package);
                // controller.MockPackageService.Setup(x => x.AddDownloadStatistics(It.IsAny<PackageStatistics>())).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
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
                var result = await controller.GetPackage(packageId, "1.0.01");

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
                var actionResult = new EmptyResult();

                var controller = new TestableApiController(MockBehavior.Strict);
                //controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.0", false)).Throws(new DataException("Can't find the database")).Verifiable();
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
                const string packageId = "Baz";
                var package = new Package() { Version = "1.2.0408", NormalizedVersion = "1.2.408" };
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(packageId, "", false)).Returns(package);
                //controller.MockPackageService.Setup(x => x.AddDownloadStatistics(It.IsAny<PackageStatistics>())).Verifiable();

                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
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
                var result = await controller.GetPackage(packageId, "");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
                controller.MockUserService.Verify();
            }

            [Fact]
            public async Task GetPackageReturns503IfNoVersionIsProvidedAndDatabaseUnavailable()
            {
                // Arrange
                const string packageId = "Baz";
                var package = new Package();
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(MockBehavior.Strict);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "", false)).Throws(new DataException("Oh noes, database broked!"));
 		        controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
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
                var result = await controller.GetPackage("Baz", "");

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
               // controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
               // controller.MockUserService.Verify();
            }
        }

        public class ThePublishPackageAction
        {
            [Fact]
            public async Task WillThrowIfAPackageWithTheIdAndNuGetVersionDoesNotExist()
            {
                // Arrange
                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("theId", "1.0.42", true)).Returns((Package)null);
                controller.SetCurrentUser(new User());

                // Act
                var result = await controller.PublishPackage("theId", "1.0.42");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"));
                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(It.IsAny<Package>(), It.IsAny<bool>()), Times.Never());
            }

            [Fact]
            public async Task WillNotListThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                // Arrange
                var owner = new User { Key = 1 };
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                    };

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("theId", "1.0.42", true)).Returns(package);
                controller.SetCurrentUser(owner);

                // Act
                var result = await controller.PublishPackage("theId", "1.0.42");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    String.Format(Strings.ApiKeyNotAuthorized, "publish"));

                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(package, It.IsAny<bool>()), Times.Never());
            }

            [Fact]
            public async Task WillListThePackageIfUserIsAnOwner()
            {
                // Arrange
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                };

                var controller = new TestableApiController();
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                controller.SetCurrentUser(owner);

                // Act
                var result = await controller.PublishPackage("theId", "1.0.42");

                // Assert
                ResultAssert.IsEmpty(result);
                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(package, It.IsAny<bool>()));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
            }
        }

        public class TheVerifyPackageKeyAction : TestContainer
        {
            [Fact]
            public void VerifyPackageKeyReturnsEmptyResultIfApiKeyExistsButIdAndVersionAreEmpty()
            {
                // Arrange
                var controller = new TestableApiController();
                controller.SetCurrentUser(new User());

                // Act
                var result = controller.VerifyPackageKey(null, null);

                // Assert
                ResultAssert.IsEmpty(result);
            }

            [Fact]
            public void VerifyPackageKeyReturns404IfPackageDoesNotExist()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                GetMock<IPackageService>()
                    .Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true))
                    .ReturnsNull();
                var controller = GetController<ApiController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.VerifyPackageKey("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    "A package with id 'foo' and version '1.0.0' does not exist.");
            }

            [Fact]
            public void VerifyPackageKeyReturns403IfUserIsNotAnOwner()
            {
                // Arrange
                var controller = new TestableApiController();
                var nonOwner = new User();
                controller.SetCurrentUser(nonOwner);
                controller.MockPackageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(
                    new Package { PackageRegistration = new PackageRegistration() });

                // Act
                var result = controller.VerifyPackageKey("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    Strings.ApiKeyNotAuthorized);
            }

            [Fact]
            public void VerifyPackageKeyReturns200IfUserIsAnOwner()
            {
                // Arrange
                var user = new User();
                var package = new Package { PackageRegistration = new PackageRegistration() };
                package.PackageRegistration.Owners.Add(user);
                var controller = new TestableApiController();
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(s => s.FindPackageByIdAndVersion("foo", "1.0.0", true)).Returns(package);

                // Act
                var result = controller.VerifyPackageKey("foo", "1.0.0");

                // Assert
                ResultAssert.IsEmpty(result);
            }
        }

        public class TheGetStatsDownloadsAction
        {
            [Fact]
            public async Task VerifyRecentPopularityStatsDownloads()
            {
                JArray report = new JArray
                {
                    new JObject
                    {
                        { "PackageId", "A" },
                        { "PackageVersion", "1.0" },
                        { "Downloads", 3 }
                    },
                    new JObject
                    {
                        { "PackageId", "A" },
                        { "PackageVersion", "1.1" },
                        { "Downloads", 4 }
                    },
                    new JObject
                    {
                        { "PackageId", "B" },
                        { "PackageVersion", "1.0" },
                        { "Downloads", 5 }
                    },
                    new JObject
                    {
                        { "PackageId", "B" },
                        { "PackageVersion", "1.1" },
                        { "Downloads", 6 }
                    },
                };

                var fakePackageVersionReport = report.ToString();

                var fakeReportService = new Mock<IReportService>();

                fakeReportService.Setup(x => x.Load("recentpopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));

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
            }

            [Fact]
            public async Task VerifyStatsDownloadsReturnsNotFoundWhenStatsNotAvailable()
            {
                var controller = new TestableApiController();
                controller.MockStatisticsService.Setup(x => x.LoadDownloadPackageVersions()).Returns(Task.FromResult(StatisticsReportResult.Failed));

                TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

                ActionResult actionResult = await controller.GetStatsDownloads(null);

                HttpStatusCodeResult httpStatusResult = (HttpStatusCodeResult)actionResult;

                Assert.True(httpStatusResult.StatusCode == (int)HttpStatusCode.NotFound, "unexpected StatusCode");
            }

            [Fact]
            public async Task VerifyRecentPopularityStatsDownloadsCount()
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

                fakeReportService.Setup(x => x.Load("recentpopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));

                var controller = new TestableApiController
                {
                    StatisticsService = new JsonStatisticsService(fakeReportService.Object),
                };

                TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

                ActionResult actionResult = await controller.GetStatsDownloads(3);

                ContentResult contentResult = (ContentResult)actionResult;

                JArray result = JArray.Parse(contentResult.Content);

                Assert.True(result.Count == 3, "unexpected content");
            }
        }
    }
}
