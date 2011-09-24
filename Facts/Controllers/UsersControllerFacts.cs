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

        public class TheForgotPasswordMethod {
            [Fact]
            public void SendsEmailWithPasswordResetUrl() {
                var messageService = new Mock<IMessageService>();
                string resetUrl = "https://example.org/?Controller=Users&Action=ResetPassword&username=somebody&token=confirmation";
                messageService.Setup(s => s.SendResetPasswordInstructions(
                    It.Is<MailAddress>(addr => addr.Address == "some@example.com" && addr.DisplayName == "somebody"),
                    resetUrl)
                );

                var user = new User {
                    EmailAddress = "some@example.com",
                    Username = "somebody",
                    PasswordResetToken = "confirmation",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GeneratePasswordResetToken("user", 1440)).Returns(user);
                var controller = CreateController(userSvc: userService, messageSvc: messageService);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                messageService.Verify(s => s.SendResetPasswordInstructions(
                    It.Is<MailAddress>(addr => addr.Address == "some@example.com" && addr.DisplayName == "somebody"),
                    resetUrl)
                );
            }

            [Fact]
            public void RedirectsAfterGeneratingToken() {
                var userService = new Mock<IUserService>();
                var user = new User { EmailAddress = "some@example.com", Username = "somebody" };
                userService.Setup(s => s.GeneratePasswordResetToken("user", 1440)).Returns(user).Verifiable();
                var controller = CreateController(userSvc: userService);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                userService.Verify(s => s.GeneratePasswordResetToken("user", 1440));
            }

            [Fact]
            public void ReturnsSameViewIfTokenGenerationFails() {
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GeneratePasswordResetToken("user", 1440)).Returns((User)null);
                var controller = CreateController(userSvc: userService);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
            }
        }

        public class TheResetPasswordMethod {
            [Fact]
            public void ShowsErrorIfTokenExpired() {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd")).Returns(false);
                var controller = CreateController(userSvc: userService);
                var model = new PasswordResetViewModel {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                var result = controller.ResetPassword("user", "token", model) as ViewResult;

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                userService.Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public void ResetsPasswordForValidToken() {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd")).Returns(true);
                var controller = CreateController(userSvc: userService);
                var model = new PasswordResetViewModel {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                var result = controller.ResetPassword("user", "token", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                userService.Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
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
