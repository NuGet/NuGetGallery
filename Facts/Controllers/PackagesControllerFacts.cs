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
using Xunit.Extensions;

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

        public class TheListPackagesMethod
        {
            [Fact]
            public void TrimsSearchTerm()
            {
                var fakeIdentity = new Mock<IIdentity>();
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(fakeIdentity: fakeIdentity, httpContext: httpContext);

                var result = controller.ListPackages(" test ") as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.Equal("test", model.SearchTerm);
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
            public void WillReturn409WhenThereIsAlreadyAnUploadInProgress()
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

                var result = controller.UploadPackage(null) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(409, result.StatusCode);
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var controller = CreateController(
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.UploadPackage(null) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileIsRequired, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillShowViewWithErrorsIfFileIsNotANuGetPackage()
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

                var result = controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileMustBeNuGetPackage, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
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
                Assert.Equal(Strings.FailedToReadUploadFile, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed()
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

                var result = controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillShowTheViewWithErrorsWhenThePackageAlreadyExists()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "theVersion"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
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
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.GetStream()).Returns(fakeFileStream);
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileSvc.Verify(x => x.SaveUploadFile(42, fakeFileStream));
                fakeFileStream.Dispose();
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

        public class TheVerifyPackageActionForGetRequests
        {
            [Fact]
            public void WillRedirectToUploadPackagePageWhenThereIsNoUploadInProgress()
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

                var result = controller.VerifyPackage() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.UploadPackage, result.RouteName);
            }

            [Fact]
            public void WillPassThePackageIdToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Id).Returns("theId");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theId", model.Id);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageVersionToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42"));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("1.0.42", model.Version);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageTitleToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Title).Returns("theTitle");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theTitle", model.Title);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageSummaryToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Summary).Returns("theSummary");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theSummary", model.Summary);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageDescriptionToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Description).Returns("theDescription");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theDescription", model.Description);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageLicenseAcceptanceRequirementToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.RequireLicenseAcceptance).Returns(true);
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.True(model.RequiresLicenseAcceptance);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageLicenseUrlToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.LicenseUrl).Returns(new Uri("http://theLicenseUri"));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("http://thelicenseuri/", model.LicenseUrl);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageTagsToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Tags).Returns("theTags");
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("theTags", model.Tags);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageProjectUrlToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.ProjectUrl).Returns(new Uri("http://theProjectUri"));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("http://theprojecturi/", model.ProjectUrl);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackagAuthorsToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Authors).Returns(new[] { "firstAuthor", "secondAuthor" });
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.Equal("firstAuthor, secondAuthor", model.Authors);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public void WillPassThePackageListedBitToTheView()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeUploadFileStream);
                var fakeNuGetPackage = new Mock<IPackage>();
                fakeNuGetPackage.Setup(x => x.Listed).Returns(true);
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var model = ((ViewResult)controller.VerifyPackage()).Model as VerifyPackageViewModel;

                Assert.True(model.Listed);
                fakeUploadFileStream.Dispose();
            }
        }

        public class TheVerifyPackageActionForPostRequests
        {
            [Fact]
            public void WillReturn404WhenThereIsNoUploadInProgress()
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

                var result = controller.VerifyPackage(null) as HttpNotFoundResult;

                Assert.NotNull(result);
            }

            [Fact]
            public void WillCreateThePackage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.VerifyPackage(null);

                fakePackageSvc.Verify(x => x.CreatePackage(fakeNuGetPackage.Object, fakeCurrentUser));
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillPublishThePackage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.VerifyPackage(null);

                fakePackageSvc.Verify(x => x.PublishPackage("theId", "theVersion"));
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillMarkThePackageUnlistedWhenListedArgumentIsFalse()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.VerifyPackage(false);

                fakePackageSvc.Verify(x => x.MarkPackageUnlisted(It.Is<Package>(p => p.PackageRegistration.Id == "theId" && p.Version == "theVersion")));
                fakeFileStream.Dispose();
            }

            [Theory]
            [InlineData(new object[] { null })]
            [InlineData(new object[] { true })]
            public void WillNotMarkThePackageUnlistedWhenListedArgumentIsNullorTrue(bool? listed)
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.VerifyPackage(listed);

                fakePackageSvc.Verify(x => x.MarkPackageUnlisted(It.IsAny<Package>()), Times.Never());
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillDeleteTheUploadFile()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.VerifyPackage(false);

                fakeUploadFileSvc.Verify(x => x.DeleteUploadFile(42));
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillSetAFlashMessage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                controller.VerifyPackage(false);

                Assert.Equal(String.Format(Strings.SuccessfullyUploadedPackage, "theId", "theVersion"), controller.TempData["Message"]);
                fakeFileStream.Dispose();
            }

            [Fact]
            public void WillRedirectToPackagePage()
            {
                var fakeCurrentUser = new User { Key = 42 };
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(fakeCurrentUser);
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileSvc.Setup(x => x.GetUploadFile(42)).Returns(fakeFileStream);
                var fakePackageSvc = new Mock<IPackageService>();
                fakePackageSvc.Setup(x => x.CreatePackage(It.IsAny<IPackage>(), It.IsAny<User>())).Returns(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<IPackage>();
                var controller = CreateController(
                    packageSvc: fakePackageSvc,
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity,
                    fakeNuGetPackage: fakeNuGetPackage);

                var result = controller.VerifyPackage(false) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.DisplayPackage, result.RouteName);
                fakeFileStream.Dispose();
            }
        }

        public class TheCancelVerifyPackageAction
        {
            [Fact]
            public void DeletesTheInProgressPackageUpload()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFile(42));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.CancelUpload() as RedirectToRouteResult;

                fakeUploadFileSvc.Verify(x => x.DeleteUploadFile(42));
            }

            [Fact]
            public void RedirectsToUploadPageAfterDelete()
            {
                var fakeUserSvc = new Mock<IUserService>();
                fakeUserSvc.Setup(x => x.FindByUsername(It.IsAny<string>())).Returns(new User { Key = 42 });
                var fakeIdentity = new Mock<IIdentity>();
                fakeIdentity.Setup(x => x.Name).Returns("theUsername");
                var fakeUploadFileSvc = new Mock<IUploadFileService>();
                fakeUploadFileSvc.Setup(x => x.DeleteUploadFile(42));
                var controller = CreateController(
                    uploadFileSvc: fakeUploadFileSvc,
                    userSvc: fakeUserSvc,
                    fakeIdentity: fakeIdentity);

                var result = controller.CancelUpload() as RedirectToRouteResult;

                Assert.False(result.Permanent);
                Assert.Equal("UploadPackage", result.RouteValues["Action"]);
                Assert.Equal("Packages", result.RouteValues["Controller"]);
            }
        }

        static PackagesController CreateController(
            Mock<IPackageService> packageSvc = null,
            Mock<IUploadFileService> uploadFileSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            Mock<HttpContextBase> httpContext = null,
            Mock<IIdentity> fakeIdentity = null,
            Mock<IPackage> fakeNuGetPackage = null,
            Mock<ISearchService> searchService = null,
            Exception readPackageException = null)
        {

            packageSvc = packageSvc ?? new Mock<IPackageService>();
            uploadFileSvc = uploadFileSvc ?? new Mock<IUploadFileService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            messageSvc = messageSvc ?? new Mock<IMessageService>();
            searchService = searchService ?? CreateSearchService();
            

            var controller = new Mock<PackagesController>(
                    packageSvc.Object,
                    uploadFileSvc.Object,
                    userSvc.Object,
                    messageSvc.Object,
                    searchService.Object);
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
                controller.Setup(x => x.ReadNuGetPackage(It.IsAny<Stream>())).Throws(readPackageException);
            else if (fakeNuGetPackage != null)
                controller.Setup(x => x.ReadNuGetPackage(It.IsAny<Stream>())).Returns(fakeNuGetPackage.Object);
            else
                controller.Setup(x => x.ReadNuGetPackage(It.IsAny<Stream>())).Returns(new Mock<IPackage>().Object);

            return controller.Object;
        }

        private static Mock<ISearchService> CreateSearchService()
        {
            var searchService = new Mock<ISearchService>();
            searchService.Setup(s => s.Search(It.IsAny<IQueryable<Package>>(), It.IsAny<string>())).Returns((IQueryable<Package> p, string searchTerm) => p);
            searchService.Setup(s => s.SearchWithRelevance(It.IsAny<IQueryable<Package>>(), It.IsAny<string>())).Returns((IQueryable<Package> p, string searchTerm) => p);

            return searchService;
        }
    }
}
