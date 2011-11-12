using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery
{
    public class PackagesControllerFacts
    {
        public class TheContactOwnersMethod
        {
            [Fact]
            public void OnlyShowsOwnersWhoAllowReceivingEmails()
            {
                var package = new PackageRegistration
                {
                    Id = "pkgid",
                    Owners = new[]{
                        new User { Username = "helpful", EmailAllowed = true},
                        new User { Username = "grinch", EmailAllowed = false},
                        new User { Username = "helpful2", EmailAllowed = true}
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
                messageService.Setup(s => s.SendContactOwnersMessage(
                    It.IsAny<MailAddress>(),
                    It.IsAny<PackageRegistration>(),
                    "I like the cut of your jib", It.IsAny<string>()));
                var package = new PackageRegistration { Id = "factory" };

                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageRegistrationById("factory")).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.User.Identity.Name).Returns("Montgomery");
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(u => u.FindByUsername("Montgomery")).Returns(new User { EmailAddress = "montgomery@burns.example.com", Username = "Montgomery" });
                var controller = CreateController(packageSvc: packageSvc,
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

        public class TheReportAbuseMethod
        {
            [Fact]
            public void SendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(s => s.ReportAbuse(
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
                var controller = CreateController(packageSvc: packageSvc,
                    messageSvc: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                {
                    Email = "frodo@hobbiton.example.com",
                    Message = "Mordor took my finger."
                };

                var result = controller.ReportAbuse("mordor", "2.0.1", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                messageService.Verify(s => s.ReportAbuse(
                    It.Is<MailAddress>(m => m.Address == "frodo@hobbiton.example.com"),
                    package,
                    "Mordor took my finger."
                ));
            }

            [Fact]
            public void SendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(s => s.ReportAbuse(
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
                var controller = CreateController(packageSvc: packageSvc,
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
                messageService.Verify(s => s.ReportAbuse(
                    It.Is<MailAddress>(m => m.Address == "frodo@hobbiton.example.com"
                    && m.DisplayName == "Frodo"),
                    package,
                    "Mordor took my finger."
                ));
            }
        }

        public class ThePublishPackageMethod
        {
            [Fact]
            public void UpdatesListedValueIfNotSelected()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0"
                };
                package.PackageRegistration.Owners.Add(new User("Frodo", "foo"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>())).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();
                packageService.Setup(svc => svc.PublishPackage("Foo", "1.0")).Verifiable();

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Frodo");

                var controller = CreateController(packageSvc: packageService, httpContext: httpContext);
                controller.Url = new UrlHelper(new RequestContext());

                // Act
                var result = controller.PublishPackage("Foo", "1.0", listed: null, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                // If we got this far, we know listing methods were not invoked.
                packageService.Verify();
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }

            [Fact]
            public void DoesNotUpdateListedValueIfSelected()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0"
                };
                package.PackageRegistration.Owners.Add(new User("Frodo", "foo"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();
                packageService.Setup(svc => svc.PublishPackage("Foo", "1.0")).Verifiable();

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Frodo");

                var controller = CreateController(packageSvc: packageService, httpContext: httpContext);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = controller.PublishPackage("Foo", "1.0", listed: true, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
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

        public class TheUploadFileActionForGetRequests
        {
            [Fact]
            public void WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeFileStream = new MemoryStream();
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.UploadPackage() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.VerifyPackage, result.RouteName);
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillShowTheViewWhenThereIsNoUploadInProgress()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns((Stream)null);
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.UploadPackage() as ViewResult;

                Assert.NotNull(result);
            }
        }

        public class TheUploadFileActionForPostRequests
        {
            [Fact]
            public void WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var controller = CreateController();

                var result = controller.UploadPackage(null) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.PackageFileIsRequired, controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillShowViewWithErrorsIfFileIsNotANuGetPackage()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.notNuPkg");
                var controller = CreateController();

                var result = controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.PackageFileMustBeNuGetPackage, controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillShowViewWithErrorsIfNuGetPackageIsInvalid()
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

                var result = controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.FailedToReadPackageFile, controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillSaveTheUploadFile()
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
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileSvc.Verify(x => x.SaveUploadFile(42, fakeFileStream));
            }

            [Fact]
            public void WillRedirectToVerifyPackageActionAfterSaving()
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
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.UploadPackage(fakeUploadedFile.Object) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.VerifyPackage, result.RouteName);
            }
        }

        static PackagesController CreateController(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IPackageService> packageSvc = null,
            Mock<IPackageFileService> packageFileSvc = null,
            Mock<IUploadFileService> uploadFileSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            Mock<HttpContextBase> httpContext = null,
            Mock<IIdentity> fakeIdentity = null,
            Exception readPackageException = null)
        {

            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            packageSvc = packageSvc ?? new Mock<IPackageService>();
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>();
            uploadFileSvc = uploadFileSvc ?? new Mock<IUploadFileService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            messageSvc = messageSvc ?? new Mock<IMessageService>();

            var controller = new Mock<PackagesController>(
                cryptoSvc.Object,
                    packageSvc.Object,
                    packageFileSvc.Object,
                    uploadFileSvc.Object,
                    userSvc.Object,
                    messageSvc.Object);
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
                controller.Setup(x => x.ReadPackage(It.IsAny<Stream>())).Throws(readPackageException);
            else
                controller.Setup(x => x.ReadPackage(It.IsAny<Stream>())).Returns(new Mock<IPackage>().Object);

            return controller.Object;
        }
    }
}
