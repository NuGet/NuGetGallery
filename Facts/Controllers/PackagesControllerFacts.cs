using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
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
    public class PackagesControllerFacts
    {
        private static PackagesController CreateController(
            Mock<IPackageService> packageSvc = null,
            Mock<IUploadFileService> uploadFileSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            Mock<HttpContextBase> httpContext = null,
            Mock<IIdentity> fakeIdentity = null,
            Mock<IPackage> fakeNuGetPackage = null,
            Mock<ISearchService> searchService = null,
            Exception readPackageException = null,
            Mock<IAutomaticallyCuratePackageCommand> autoCuratePackageCmd = null,
            Mock<INuGetExeDownloaderService> downloaderSvc = null,
            Mock<IConfiguration> config = null,
            Mock<ICacheService> cacheSvc = null,
            Mock<IPackageFileService> packageFileSvc = null)
        {
            packageSvc = packageSvc ?? new Mock<IPackageService>();
            if (uploadFileSvc == null)
            {
                uploadFileSvc = new Mock<IUploadFileService>();
                uploadFileSvc.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                uploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                uploadFileSvc.Setup(x => x.SaveUploadFileAsync(42, It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }
            userSvc = userSvc ?? new Mock<IUserService>();
            messageSvc = messageSvc ?? new Mock<IMessageService>();
            searchService = searchService ?? CreateSearchService();
            autoCuratePackageCmd = autoCuratePackageCmd ?? new Mock<IAutomaticallyCuratePackageCommand>();
            downloaderSvc = downloaderSvc ?? new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
            config = config ?? new Mock<IConfiguration>();
            cacheSvc = cacheSvc ?? new Mock<ICacheService>();
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>();

            var controller = new Mock<PackagesController>(
                packageSvc.Object,
                uploadFileSvc.Object,
                userSvc.Object,
                messageSvc.Object,
                searchService.Object,
                cacheSvc.Object,
                autoCuratePackageCmd.Object,
                downloaderSvc.Object,
                config.Object,
                packageFileSvc.Object);
            controller.CallBase = true;

            if (httpContext != null)
            {
                TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, controller.Object);
            }

            if (fakeIdentity != null)
            {
                controller.Setup(x => x.GetIdentity()).Returns(fakeIdentity.Object);
            }

            if (readPackageException != null)
            {
                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Throws(readPackageException);
            }
            else if (fakeNuGetPackage != null)
            {
                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Returns(fakeNuGetPackage.Object);
            }
            else
            {
                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Returns(new Mock<IPackage>().Object);
            }

            return controller.Object;
        }

        private static Mock<ISearchService> CreateSearchService()
        {
            var searchService = new Mock<ISearchService>();
            int total;
            searchService.Setup(s => s.Search(It.IsAny<IQueryable<Package>>(), It.IsAny<SearchFilter>(), out total)).Returns(
                (IQueryable<Package> p, string searchTerm) => p);

            return searchService;
        }

        public class TheCancelVerifyPackageAction
        {
            [Fact]
            public async Task DeletesTheInProgressPackageUpload()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                await controller.CancelUpload();

                fakeUploadFileSvc.Verify(x => x.DeleteUploadFileAsync(42));
            }

            [Fact]
            public async Task RedirectsToUploadPageAfterDelete()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");

                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.CancelUpload() as RedirectToRouteResult;

                Assert.False(result.Permanent);
                Assert.Equal("UploadPackage", result.RouteValues["Action"]);
                // TODO: Figure out why the RouteValues collection no longer contains "Controller" parameter
                //Assert.Equal("Packages", result.RouteValues["Controller"]);
            }
        }

        public class TheConfirmOwnerMethod
        {
            [Fact]
            public void WithEmptyTokenReturnsHttpNotFound()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername("username")).Returns(new User { Username = "username" });
                var controller = CreateController(packageSvc: packageService, userSvc: userService);

                var result = controller.ConfirmOwner("foo", "username", "");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WithNonExistentPackageIdReturnsHttpNotFound()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername("username")).Returns(new User { Username = "username" });
                var controller = CreateController(userSvc: userService);

                var result = controller.ConfirmOwner("foo", "username", "token");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WithNonExistentUserReturnsHttpNotFound()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var controller = CreateController(packageSvc: packageService);

                var result = controller.ConfirmOwner("foo", "username", "token");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void RequiresUserBeLoggedInToConfirm()
            {
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                packageService.Setup(p => p.ConfirmPackageOwner(package, user, "token")).Returns(true);
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.User.Identity.Name).Returns("not-username");
                var controller = CreateController(packageSvc: packageService, userSvc: userService, httpContext: httpContext);

                var result = controller.ConfirmOwner("foo", "username", "token") as HttpStatusCodeResult;

                Assert.NotNull(result);
            }

            [Fact]
            public void WithValidTokenConfirmsUser()
            {
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                packageService.Setup(p => p.ConfirmPackageOwner(package, user, "token")).Returns(true);
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.User.Identity.Name).Returns("username");
                var controller = CreateController(packageSvc: packageService, userSvc: userService, httpContext: httpContext);


                var model = (controller.ConfirmOwner("foo", "username", "token") as ViewResult).Model as PackageOwnerConfirmationModel;

                Assert.True(model.Success);
                Assert.Equal("foo", model.PackageId);
            }
        }

        public class TheContactOwnersMethod
        {
            [Fact]
            public void OnlyShowsOwnersWhoAllowReceivingEmails()
            {
                var package = new PackageRegistration
                {
                    Id = "pkgid",
                    Owners = new[]
                            {
                                new User { Username = "helpful", EmailAllowed = true },
                                new User { Username = "grinch", EmailAllowed = false },
                                new User { Username = "helpful2", EmailAllowed = true }
                            }
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageRegistrationById("pkgid")).Returns(package);
                var controller = CreateController(packageSvc: packageSvc);

                var model = (controller.ContactOwners("pkgid") as ViewResult).Model as ContactOwnersViewModel;

                Assert.Equal(2, model.Owners.Count());
                Assert.Empty(model.Owners.Where(u => u.Username == "grinch"));
            }

            [Fact]
            public void CallsSendContactOwnersMessageWithUserInfo()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.SendContactOwnersMessage(
                        It.IsAny<MailAddress>(),
                        It.IsAny<PackageRegistration>(),
                        "I like the cut of your jib",
                        It.IsAny<string>()));
                var package = new PackageRegistration { Id = "factory" };

                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageRegistrationById("factory")).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.User.Identity.Name).Returns("Montgomery");
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(u => u.FindByUsername("Montgomery")).Returns(
                    new User { EmailAddress = "montgomery@burns.example.com", Username = "Montgomery" });
                var controller = CreateController(
                    packageSvc: packageSvc,
                    messageSvc: messageService,
                    userSvc: userSvc,
                    httpContext: httpContext);
                var model = new ContactOwnersViewModel
                {
                    Message = "I like the cut of your jib",
                };

                var result = controller.ContactOwners("factory", model) as RedirectToRouteResult;

                Assert.NotNull(result);
            }
        }

        public class TheEditMethod
        {
            [Fact]
            public void UpdatesUnlistedIfSelected()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(new User("Frodo", "foo"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>())).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Frodo");

                var controller = CreateController(packageSvc: packageService, httpContext: httpContext);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = controller.Edit("Foo", "1.0", listed: false, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }

            [Fact]
            public void UpdatesUnlistedIfNotSelected()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(new User("Frodo", "foo"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>())).Verifiable();
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Frodo");

                var controller = CreateController(packageSvc: packageService, httpContext: httpContext);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = controller.Edit("Foo", "1.0", listed: true, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }
        }

        public class TheListPackagesMethod
        {
            [Fact]
            public void TrimsSearchTerm()
            {
                var fakeIdentity = new Mock<IIdentity>();
                var httpContext = new Mock<HttpContextBase>();
                var searchService = new Mock<ISearchService>();

                var controller = CreateController(fakeIdentity: fakeIdentity, httpContext: httpContext, searchService: searchService);

                var result = controller.ListPackages(" test ") as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.Equal("test", model.SearchTerm);
            }
        }

        public class TheReportAbuseMethod
        {
            [Fact]
            public void SendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.ReportAbuse(
                        It.IsAny<MailAddress>(),
                        It.IsAny<Package>(),
                        "Mordor took my finger"));
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageByIdAndVersion("mordor", "2.0.1", true)).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(false);
                var controller = CreateController(
                    packageSvc: packageSvc,
                    messageSvc: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                {
                    Email = "frodo@hobbiton.example.com",
                    Message = "Mordor took my finger."
                };

                var result = controller.ReportAbuse("mordor", "2.0.1", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<MailAddress>(m => m.Address == "frodo@hobbiton.example.com"),
                        package,
                        "Mordor took my finger."
                             ));
            }

            [Fact]
            public void SendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.ReportAbuse(
                        It.IsAny<MailAddress>(),
                        It.IsAny<Package>(),
                        "Mordor took my finger"));
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageByIdAndVersion("mordor", It.IsAny<string>(), true)).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Frodo");
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(u => u.FindByUsername("Frodo")).Returns(new User { EmailAddress = "frodo@hobbiton.example.com", Username = "Frodo" });
                var controller = CreateController(
                    packageSvc: packageSvc,
                    messageSvc: messageService,
                    userSvc: userSvc,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                {
                    Message = "Mordor took my finger."
                };

                var result = controller.ReportAbuse("mordor", "2.0.1", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                userSvc.VerifyAll();
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<MailAddress>(
                            m => m.Address == "frodo@hobbiton.example.com"
                                 && m.DisplayName == "Frodo"),
                        package,
                        "Mordor took my finger."
                             ));
            }
        }

        public class TheUploadFileActionForGetRequests
        {
            [Fact]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeFileStream = new MemoryStream();
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.VerifyPackage, result.RouteName);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillShowTheViewWhenThereIsNoUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage() as ViewResult;

                Assert.NotNull(result);
            }
        }

        public class TheUploadFileActionForPostRequests
        {
            [Fact]
            public async Task WillReturn409WhenThereIsAlreadyAnUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeFileStream = new MemoryStream();
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage(null) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(409, result.StatusCode);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var controller = CreateController(
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage(null) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileIsRequired, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfFileIsNotANuGetPackage()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.notNuPkg");
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var controller = CreateController(
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileMustBeNuGetPackage, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfNuGetPackageIsInvalid()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var readPackageException = new Exception();
                var controller = CreateController(
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    readPackageException: readPackageException);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.FailedToReadUploadFile, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakePackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { new User { Key = 1 /* not the current user */ } } };
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExists()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(
                    String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "theVersion"),
                    controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillSaveTheUploadFile()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = new MemoryStream();
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.GetStream()).Returns(fakeFileStream);
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileSvc.Setup(x => x.SaveUploadFileAsync(42, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileSvc.Verify(x => x.SaveUploadFileAsync(42, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillRedirectToVerifyPackageActionAfterSaving()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = new MemoryStream();
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileSvc.Setup(x => x.SaveUploadFileAsync(42, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.VerifyPackage, result.RouteName);
            }
        }

        public class TheVerifyPackageActionForGetRequests
        {
            [Fact]
            public async Task WillRedirectToUploadPackagePageWhenThereIsNoUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns<Stream>(null);
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.VerifyPackage() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.UploadPackage, result.RouteName);
            }

            [Fact]
            public async Task WillPassThePackageIdToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Id).Returns("theId");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theId", model.Id);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageVersionToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("1.0.42", model.Version);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageTitleToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Title).Returns("theTitle");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theTitle", model.Title);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageSummaryToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Summary).Returns("theSummary");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theSummary", model.Summary);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageDescriptionToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Description).Returns("theDescription");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theDescription", model.Description);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageLicenseAcceptanceRequirementToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.RequireLicenseAcceptance).Returns(true);
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.True(model.RequiresLicenseAcceptance);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageLicenseUrlToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.LicenseUrl).Returns(new Uri("http://theLicenseUri"));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("http://thelicenseuri/", model.LicenseUrl);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageTagsToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Tags).Returns("theTags");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theTags", model.Tags);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageProjectUrlToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.ProjectUrl).Returns(new Uri("http://theProjectUri"));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("http://theprojecturi/", model.ProjectUrl);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackagAuthorsToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Authors).Returns(new[] { "firstAuthor", "secondAuthor" });
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("firstAuthor, secondAuthor", model.Authors);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageListedBitToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Listed).Returns(true);
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.True(model.Listed);
                fakeUploadFileStream.Dispose();
            }
        }

        public class TheVerifyPackageActionForPostRequests
        {
            [Fact]
            public async Task WillReturn404WhenThereIsNoUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = await controller.VerifyPackage(null) as HttpNotFoundResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillCreateThePackage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.VerifyPackage(null);

                fakePackageSvc.Verify(x => x.CreatePackageAsync(fakeNuGetPackage.Object, fakeCurrentUser));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillPublishThePackage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.VerifyPackage(null);

                fakePackageSvc.Verify(x => x.PublishPackage("theId", "theVersion"));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillMarkThePackageUnlistedWhenListedArgumentIsFalse()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.VerifyPackage(false);

                fakePackageSvc.Verify(
                    x => x.MarkPackageUnlisted(It.Is<Package>(p => p.PackageRegistration.Id == "theId" && p.Version == "theVersion")));
                fakeFileStream.Dispose();
            }

            [Theory]
            [InlineData(new object[] { null })]
            [InlineData(new object[] { true })]
            public async Task WillNotMarkThePackageUnlistedWhenListedArgumentIsNullorTrue(bool? listed)
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.VerifyPackage(listed);

                fakePackageSvc.Verify(x => x.MarkPackageUnlisted(It.IsAny<Package>()), Times.Never());
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillDeleteTheUploadFile()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0)).Verifiable();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.VerifyPackage(false);

                fakeUploadFileSvc.Verify();
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillSetAFlashMessage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileSvc.Setup(x => x.SaveUploadFileAsync(42, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                await controller.VerifyPackage(false);

                Assert.Equal(String.Format(Strings.SuccessfullyUploadedPackage, "theId", "theVersion"), controller.TempData["Message"]);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillRedirectToPackagePage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(
                    Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var result = await controller.VerifyPackage(false) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.DisplayPackage, result.RouteName);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillCurateThePackage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var fakePackageSvc = new Mock<IPackageService>();
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(Task.FromResult(fakePackage));
                var fakeNuGetPackage = new Mock<IPackage>();
                var fakeAutoCuratePackageCmd = new Mock<IAutomaticallyCuratePackageCommand>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage,
                    autoCuratePackageCmd: fakeAutoCuratePackageCmd);

                await controller.VerifyPackage(false);

                fakeAutoCuratePackageCmd.Verify(fake => fake.Execute(fakePackage, fakeNuGetPackage.Object));
            }

            [Fact]
            public async Task WillExtractNuGetExe()
            {
                // Arrange
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(Stream.Null));
                var fakePackageSvc = new Mock<IPackageService>();
                var commandLinePackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "NuGet.CommandLine" },
                    Version = "2.0.0",
                    IsLatestStable = true
                };
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(Task.FromResult(commandLinePackage));
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                nugetExeDownloader.Setup(d => d.UpdateExecutableAsync(It.IsAny<IPackage>())).Returns(Task.FromResult(0)).Verifiable();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    fakeIdentity: fakeIdentity,
                    userSvc: fakeUserSvc,
                    downloaderSvc: nugetExeDownloader);

                // Act
                await controller.VerifyPackage(false);

                // Assert
                nugetExeDownloader.Verify();
            }

            [Fact]
            public async Task WillNotExtractNuGetExeIfIsNotLatestStable()
            {
                // Arrange
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();

                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                var fakePackageSvc = new Mock<IPackageService>();
                var commandLinePackage = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "NuGet.CommandLine" },
                    Version = "2.0.0",
                    IsLatestStable = false
                };
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(Task.FromResult(commandLinePackage));
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    fakeIdentity: fakeIdentity,
                    userSvc: fakeUserSvc,
                    downloaderSvc: nugetExeDownloader);

                // Act
                await controller.VerifyPackage(false);

                // Assert
                nugetExeDownloader.Verify(d => d.UpdateExecutableAsync(It.IsAny<IPackage>()), Times.Never());
            }

            [Theory]
            [InlineData("nuget-commandline")]
            [InlineData("nuget..commandline")]
            [InlineData("nuget.command")]
            public async Task WillNotExtractNuGetExeIfIsItDoesNotMatchId(string id)
            {
                // Arrange
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();

                fakeUploadFileSvc.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                var fakePackageSvc = new Mock<IPackageService>();
                var commandLinePackage = new Package { PackageRegistration = new PackageRegistration { Id = id }, Version = "2.0.0", IsLatestStable = true };
                fakePackageSvc.Setup(x => x.CreatePackageAsync(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(Task.FromResult(commandLinePackage));
                var nugetExeDownloader = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict);
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    fakeIdentity: fakeIdentity,
                    userSvc: fakeUserSvc,
                    downloaderSvc: nugetExeDownloader);

                // Act
                await controller.VerifyPackage(false);

                // Assert
                nugetExeDownloader.Verify(d => d.UpdateExecutableAsync(It.IsAny<IPackage>()), Times.Never());
            }
        }
    }
}
