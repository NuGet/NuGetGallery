using System;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class UsersControllerFacts
    {
        public class TheRegisterMethod
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = CreateController();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.Register(null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void WillCreateTheUser()
            {
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User() { Username = "theUsername", EmailAddress = "to@example.com" });
                var controller = CreateController(userSvc: userSvc);

                controller.Register(new RegisterRequest()
                {
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
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new EntityException("aMessage"));
                var controller = CreateController(userSvc: userSvc);

                var result = controller.Register(new RegisterRequest()
                {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                }) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillSendNewUserEmailIfConfirmationRequired()
            {
                var messageSvc = new Mock<IMessageService>();
                string sentConfirmationUrl = null;
                MailAddress sentToAddress = null;
                messageSvc.Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Callback<MailAddress, string>((to, url) =>
                    {
                        sentToAddress = to;
                        sentConfirmationUrl = url;
                    });
                var userSvc = new Mock<IUserService>();
                userSvc
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User()
                    {
                        Username = "theUsername",
                        EmailAddress = "to@example.com",
                        EmailConfirmationToken = "confirmation"
                    });
                var settings = new GallerySetting { ConfirmEmailAddresses = true };
                var controller = CreateController(settings: settings, userSvc: userSvc, messageSvc: messageSvc);

                controller.Register(new RegisterRequest()
                {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "to@example.com",
                });

                // We use a catch-all route for unit tests so we can see the parameters 
                // are passed correctly.
                Assert.Equal("https://example.org/?Controller=Users&Action=Confirm&username=theUsername&token=confirmation", sentConfirmationUrl);
                Assert.Equal("to@example.com", sentToAddress.Address);
            }
        }

        public class TheEditMethod
        {
            [Fact]
            public void UpdatesEmailAllowedSetting()
            {
                var user = new User
                {
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername(It.IsAny<string>())).Returns(user);
                userService.Setup(u => u.UpdateProfile(user, "test@example.com", false)).Verifiable();
                var controller = CreateController(userSvc: userService);
                var model = new EditProfileViewModel { EmailAddress = "test@example.com", EmailAllowed = false };

                var result = controller.Edit(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                userService.Verify(u => u.UpdateProfile(user, "test@example.com", false));
                Assert.Equal("Account settings saved!", controller.TempData["Message"]);
            }

            [Fact]
            public void SendsEmailChangeConfirmationNoticeWhenEmailConfirmationTokenChanges()
            {
                var user = new User
                {
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername(It.IsAny<string>())).Returns(user);
                userService.Setup(u => u.UpdateProfile(user, "new@example.com", true)).Callback(() => user.EmailConfirmationToken = "token");
                var controller = CreateController(userSvc: userService);
                var model = new EditProfileViewModel { EmailAddress = "new@example.com", EmailAllowed = true };

                var result = controller.Edit(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal("Account settings saved! We sent a confirmation email to verify your new email. When you confirm the email address, it will take effect and we will forget the old one.", controller.TempData["Message"]);
            }

            [Fact]
            public void DoesNotSendEmailChangeConfirmationNoticeWhenTokenDoesNotChange()
            {
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    EmailAllowed = true,
                    EmailConfirmationToken = "token"
                };

                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername(It.IsAny<string>())).Returns(user);
                userService.Setup(u => u.UpdateProfile(user, It.IsAny<string>(), true));
                var messageService = new Mock<IMessageService>();
                messageService.Setup(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>())).Throws(new InvalidOperationException());
                var controller = CreateController(userSvc: userService, messageSvc: messageService);
                var model = new EditProfileViewModel { EmailAddress = "old@example.com", EmailAllowed = true };

                var result = controller.Edit(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal("Account settings saved!", controller.TempData["Message"]);
            }

            [Fact]
            public void WithInvalidUsernameReturnsFileNotFound()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername(It.IsAny<string>())).Returns((User)null);
                var controller = CreateController(userSvc: userService);

                var result = controller.Edit(new EditProfileViewModel()) as HttpNotFoundResult;

                Assert.NotNull(result);
            }
        }

        public class TheGenerateApiKeyMethod
        {
            [Fact]
            public void RedirectsToAccountPage()
            {
                var currentUser = new Mock<IPrincipal>();
                currentUser.Setup(u => u.Identity.Name).Returns("the-username");
                var controller = CreateController(currentUser: currentUser);

                var result = controller.GenerateApiKey() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal("Account", result.RouteValues["action"]);
                Assert.Equal("Users", result.RouteValues["controller"]);
            }

            [Fact]
            public void GeneratesAnApiKey()
            {
                var currentUser = new Mock<IPrincipal>();
                currentUser.Setup(u => u.Identity.Name).Returns("the-username");
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GenerateApiKey("the-username")).Verifiable();
                var controller = CreateController(userSvc: userService, currentUser: currentUser);

                var result = controller.GenerateApiKey() as RedirectToRouteResult;

                userService.VerifyAll();
            }
        }

        public class TheConfirmMethod
        {
            [Fact]
            public void Returns404WhenTokenIsEmpty()
            {
                var controller = CreateController();

                var result = controller.Confirm("username", "") as HttpNotFoundResult;

                Assert.NotNull(result);
            }

            [Fact]
            public void ReturnsConfirmedWhenTokenMatchesUser()
            {
                var user = new User
                {
                    UnconfirmedEmailAddress = "email@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                userService.Setup(u => u.ConfirmEmailAddress(user, "the-token")).Returns(true);
                var controller = CreateController(userSvc: userService);

                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.True(model.SuccessfulConfirmation);
            }

            [Fact]
            public void SendsAccountChangedNoticeWhenConfirmingChangedEmail()
            {
                var userService = new Mock<IUserService>();
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                userService.Setup(u => u.ConfirmEmailAddress(user, "the-token")).Returns(true);
                var messageService = new Mock<IMessageService>();
                messageService.Setup(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com")).Verifiable();
                var controller = CreateController(messageSvc: messageService, userSvc: userService);

                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.True(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                messageService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com"));
            }

            [Fact]
            public void ReturnsFalseWhenTokenDoesNotMatchUser()
            {
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                userService.Setup(u => u.ConfirmEmailAddress(user, "not-the-token")).Returns(false);
                var controller = CreateController(userSvc: userService);

                var model = (controller.Confirm("username", "not-the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.False(model.SuccessfulConfirmation);
            }
        }

        public class TheForgotPasswordMethod
        {
            [Fact]
            public void SendsEmailWithPasswordResetUrl()
            {
                var user = new User
                {
                    EmailAddress = "some@example.com",
                    Username = "somebody",
                    PasswordResetToken = "confirmation",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                var messageService = new Mock<IMessageService>();
                string resetUrl = "https://example.org/?Controller=Users&Action=ResetPassword&username=somebody&token=confirmation";
                messageService.Setup(s => s.SendPasswordResetInstructions(user, resetUrl)
                );
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GeneratePasswordResetToken("user", 1440)).Returns(user);
                var controller = CreateController(userSvc: userService, messageSvc: messageService);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                messageService.Verify(s => s.SendPasswordResetInstructions(user, resetUrl));
            }

            [Fact]
            public void RedirectsAfterGeneratingToken()
            {
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
            public void ReturnsSameViewIfTokenGenerationFails()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GeneratePasswordResetToken("user", 1440)).Returns((User)null);
                var controller = CreateController(userSvc: userService);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
            }
        }

        public class TheResetPasswordMethod
        {
            [Fact]
            public void ShowsErrorIfTokenExpired()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd")).Returns(false);
                var controller = CreateController(userSvc: userService);
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                var result = controller.ResetPassword("user", "token", model) as ViewResult;

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                userService.Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public void ResetsPasswordForValidToken()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd")).Returns(true);
                var controller = CreateController(userSvc: userService);
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                var result = controller.ResetPassword("user", "token", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                userService.Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }
        }

        public class TheThanksMethod
        {
            [Fact]
            public void ShowsDefaultThanksViewWhenConfirmingEmailAddressIsRequired()
            {
                var settings = new GallerySetting { ConfirmEmailAddresses = true };
                var controller = CreateController(settings: settings);

                var result = controller.Thanks() as ViewResult;

                Assert.Empty(result.ViewName);
                Assert.Null(result.Model);
            }

            [Fact]
            public void ShowsConfirmViewWithModelWhenConfirmingEmailAddressIsNotRequired()
            {
                var settings = new GallerySetting { ConfirmEmailAddresses = false };
                var controller = CreateController(settings: settings);

                var result = controller.Thanks() as ViewResult;

                Assert.Equal("Confirm", result.ViewName);
                var model = result.Model as EmailConfirmationModel;
                Assert.True(model.ConfirmingNewAccount);
                Assert.True(model.SuccessfulConfirmation);
            }
        }

        static UsersController CreateController(
            GallerySetting settings = null,
            Mock<IFormsAuthenticationService> formsAuthSvc = null,
            Mock<IUserService> userSvc = null,
            Mock<IMessageService> messageSvc = null,
            Mock<IPrincipal> currentUser = null)
        {
            formsAuthSvc = formsAuthSvc ?? new Mock<IFormsAuthenticationService>();
            userSvc = userSvc ?? new Mock<IUserService>();
            var packageService = new Mock<IPackageService>();
            messageSvc = messageSvc ?? new Mock<IMessageService>();
            settings = settings ?? new GallerySetting();

            if (currentUser == null)
            {
                currentUser = new Mock<IPrincipal>();
                currentUser.Setup(u => u.Identity.Name).Returns((string)null);
            }

            var controller = new UsersController(
                userSvc.Object,
                packageService.Object,
                messageSvc.Object,
                settings,
                currentUser.Object);

            TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), controller);
            return controller;
        }
    }
}
