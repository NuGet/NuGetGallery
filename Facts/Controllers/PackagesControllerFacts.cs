using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery.Controllers {
    public class PackagesControllerFacts {
        public class TheContactOwnersMethod {
            [Fact]
            public void CallsSendContactOwnersMessageWithUserInfo() {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(s => s.SendContactOwnersMessage(
                    It.IsAny<MailAddress>(),
                    It.IsAny<PackageRegistration>(),
                    "I like the cut of your jib"));
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
                var model = new ContactOwnersViewModel {
                    Message = "I like the cut of your jib",
                };

                var result = controller.ContactOwners("factory", model) as RedirectToRouteResult;

                Assert.NotNull(result);
            }
        }

        public class TheReportAbuseMethod {
            [Fact]
            public void SendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated() {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(s => s.ReportAbuse(
                    It.IsAny<MailAddress>(),
                    It.IsAny<Package>(),
                    "Mordor took my finger"));
                var package = new Package {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageByIdAndVersion("mordor", "2.0.1")).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(false);
                var controller = CreateController(packageSvc: packageSvc,
                    messageSvc: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel {
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
            public void SendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated() {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(s => s.ReportAbuse(
                    It.IsAny<MailAddress>(),
                    It.IsAny<Package>(),
                    "Mordor took my finger"));
                var package = new Package {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageSvc = new Mock<IPackageService>();
                packageSvc.Setup(p => p.FindPackageByIdAndVersion("mordor", It.IsAny<string>())).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Frodo");
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(u => u.FindByUsername("Frodo")).Returns(new User { EmailAddress = "frodo@hobbiton.example.com", Username = "Frodo" });
                var controller = CreateController(packageSvc: packageSvc,
                    messageSvc: messageService,
                    userSvc: userSvc,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel {
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

        static PackagesController CreateController(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IPackageService> packageSvc = null,
            Mock<IPackageFileService> packageFileSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            Mock<HttpContextBase> httpContext = null) {

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

            if (httpContext != null) {
                var controllerContext = new ControllerContext(httpContext.Object, new RouteData(), controller);
                controller.ControllerContext = controllerContext;
            }

            return controller;
        }

    }
}
