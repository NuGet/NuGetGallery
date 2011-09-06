using System;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery {
    public class UsersControllerFacts {
        public class TheRegisterMethod {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid() {
                var controller = CreateController();
                controller.ModelState.AddModelError(string.Empty, "aFakeError");

                var result = controller.Register(null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void WillCreateTheUser() {
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() { Username = "theUsername", EmailAddress = "to@example.com" });
                var controller = CreateController(userSvc: userSvc);

                controller.Register(new RegisterRequest() {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                });

                userSvc.Verify(x => x.Create(
                    "theUsername",
                    "thePassword",
                    "theEmailAddress"));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow() {
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new EntityException("aMessage"));
                var controller = CreateController(userSvc: userSvc);

                var result = controller.Register(new RegisterRequest() {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                }) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillSendNewUserEmail() {
                var messageSvc = new Mock<IMessageService>();
                messageSvc.Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>())).Verifiable();
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() {
                        Username = "theUsername",
                        EmailAddress = "to@example.com",
                        ConfirmationToken = "confirmation"
                    });
                var controller = CreateController(userSvc: userSvc, messageSvc: messageSvc);

                controller.Register(new RegisterRequest() {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "to@example.com",
                });

                // We use a catch-all route for unit tests so we can see the parameters 
                // are passed correctly.
                messageSvc.Verify(x => x.SendNewAccountEmail(
                    It.Is<MailAddress>(m => m.Address == "to@example.com"), "https://example.org/?Controller=Users&Action=Confirm&token=confirmation"));
            }
        }

        public class TheGenerateApiKeyMethod {
            [Fact]
            public void RedirectsToAccountPage() {
                var controller = CreateController(currentUserName: "the-username");

                var result = controller.GenerateApiKey() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal("Account", result.RouteValues["action"]);
                Assert.Equal("Users", result.RouteValues["controller"]);
            }

            [Fact]
            public void GeneratesAnApiKey() {
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GenerateApiKey("the-username")).Verifiable();
                var controller = CreateController(userSvc: userService, currentUserName: "the-username");

                var result = controller.GenerateApiKey() as RedirectToRouteResult;

                userService.VerifyAll();
            }
        }

        public class TheConfirmMethod {
            [Fact]
            public void ReturnsNullConfirmedWhenTokenIsEmpty() {
                var controller = CreateController();

                var result = controller.Confirm("") as ViewResult;

                Assert.Null(result.ViewBag.Confirmed);
            }

            [Fact]
            public void ReturnsConfirmedWhenTokenIsMatchesUser() {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ConfirmAccount("the-token")).Returns(true);
                var controller = CreateController(userSvc: userService);

                var result = controller.Confirm("the-token") as ViewResult;

                Assert.True(result.ViewBag.Confirmed);
            }

            [Fact]
            public void ReturnsFalseWhenTokenDoesNotMatchUser() {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ConfirmAccount("the-token")).Returns(false);
                var controller = CreateController(userSvc: userService);

                var result = controller.Confirm("the-token") as ViewResult;

                Assert.False(result.ViewBag.Confirmed);
            }
        }

        static UsersController CreateController(
            Mock<IFormsAuthenticationService> formsAuthSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            string currentUserName = null) {
            formsAuthSvc = formsAuthSvc ?? new Mock<IFormsAuthenticationService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            var packageService = new Mock<IPackageService>();
            messageSvc = messageSvc ?? new Mock<IMessageService>();

            var controller = new UsersController(
                formsAuthSvc.Object,
                userSvc.Object,
                packageService.Object,
                messageSvc.Object);

            // TODO: See this following block? This is a code smell. We
            //       need a better way to grab the current username perhaps?

            var httpContext = new Mock<HttpContextBase>();
            if (currentUserName != null) {
                httpContext.Setup(c => c.User.Identity.Name).Returns(currentUserName);
            }
            httpContext.Setup(c => c.Request.Url).Returns(new Uri("https://example.org/"));
            httpContext.Setup(c => c.Request.ApplicationPath).Returns("/");
            httpContext.Setup(c => c.Response.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
            var requestContext = new RequestContext(httpContext.Object, new RouteData());
            var controllerContext = new ControllerContext(requestContext, controller);
            controller.ControllerContext = controllerContext;
            var routeCollection = new RouteCollection();
            routeCollection.MapRoute("catch-all", "{*catchall}");
            controller.Url = new UrlHelper(requestContext, routeCollection);

            return controller;
        }
    }
}
