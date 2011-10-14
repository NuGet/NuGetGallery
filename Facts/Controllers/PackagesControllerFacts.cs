using System;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery.Controllers
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

        [Fact]
        public void PublishPackageUpdatesListedValueIfNotSelected()
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
        public void PublishPackageDoesNotUpdateListedValueIfSelected()
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

        [Fact]
        public void EditControllerUpdatesUnlistedIfSelected()
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
        public void EditControllerUpdatesUnlistedIfNotSelected()
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

        static PackagesController CreateController(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IPackageService> packageSvc = null,
            Mock<IPackageFileService> packageFileSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            Mock<HttpContextBase> httpContext = null)
        {

            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            packageSvc = packageSvc ?? new Mock<IPackageService>();
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            messageSvc = messageSvc ?? new Mock<IMessageService>();

            var controller = new PackagesController(
                    cryptoSvc.Object,
                    packageSvc.Object,
                    packageFileSvc.Object,
                    userSvc.Object,
                    messageSvc.Object
                );

            if (httpContext != null)
            {
                TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, controller);
            }

            return controller;
        }
    }
}
