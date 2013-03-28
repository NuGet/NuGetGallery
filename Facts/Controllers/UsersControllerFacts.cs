using System;
using System.Linq;
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
        private static UsersController CreateController(
            Mock<IConfiguration> config = null,
            Mock<IUserService> userService = null,
            Mock<IMessageService> messageService = null,
            Mock<ICuratedFeedsByManagerQuery> feedsQuery = null,
            Mock<IPrincipal> currentUser = null)

        {
            userService = userService ?? new Mock<IUserService>();
            var packageService = new Mock<IPackageService>();
            messageService = messageService ?? new Mock<IMessageService>();
            config = config ?? new Mock<IConfiguration>();
            feedsQuery = feedsQuery ?? new Mock<ICuratedFeedsByManagerQuery>();

            if (currentUser == null)
            {
                currentUser = new Mock<IPrincipal>();
                currentUser.Setup(u => u.Identity.Name).Returns((string)null);
            }

            var controller = new UsersController(
                feedsQuery.Object,
                userService.Object,
                packageService.Object,
                messageService.Object,
                config.Object,
                currentUser.Object);

            TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), controller);
            return controller;
        }

        public class TheAccountAction
        {
            [Fact]
            public void WillGetTheCurrentUserUsingTheRequestIdentityName()
            {
                var userService = new Mock<IUserService>();
                var user = new Mock<IPrincipal>();
                var identityStub = new Mock<IIdentity>();
                user.Setup(stub => stub.Identity).Returns(identityStub.Object);
                identityStub.Setup(stub => stub.Name).Returns("theUsername");
                userService
                    .Setup(s => s.FindByUsername(It.IsAny<string>()))
                    .Returns(new User { Key = 42 });
                var controller = CreateController(userService: userService, currentUser: user);

                //act
                controller.Account();

                // verify
                userService.Verify(stub => stub.FindByUsername("theUsername"));
            }

            [Fact]
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                var feedsQuery = new Mock<ICuratedFeedsByManagerQuery>();
                var userService = new Mock<IUserService>();
                userService
                    .Setup(s => s.FindByUsername(It.IsAny<string>()))
                    .Returns(new User { Key = 42 });
                var controller = CreateController(feedsQuery: feedsQuery, userService: userService);

                // act
                controller.Account();

                // verify
                feedsQuery.Verify(query => query.Execute(42));
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheUserApiKey()
            {
                var stubApiKey = Guid.NewGuid();
                var userService = new Mock<IUserService>();
                userService
                    .Setup(s => s.FindByUsername(It.IsAny<string>()))
                    .Returns(new User { Key = 42, ApiKey = stubApiKey });
                var controller = CreateController(userService: userService);

                // act
                var model = ((ViewResult)controller.Account()).Model as AccountViewModel;

                // verify
                Assert.Equal(stubApiKey.ToString(), model.ApiKey);
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                var feedsQuery = new Mock<ICuratedFeedsByManagerQuery>();
                var userService = new Mock<IUserService>();
                userService
                    .Setup(s => s.FindByUsername(It.IsAny<string>()))
                    .Returns(new User { Key = 42 });
                feedsQuery
                    .Setup(stub => stub.Execute(It.IsAny<int>()))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });
                var controller = CreateController(feedsQuery: feedsQuery, userService: userService);

                // act
                var model = ((ViewResult)controller.Account()).Model as AccountViewModel;

                // verify
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
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
                var controller = CreateController(userService: userService);

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
                var controller = CreateController(messageService: messageService, userService: userService);

                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.True(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                messageService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com"));
            }

            [Fact]
            public void DoesntSendAccountChangedEmailsWhenNoOldConfirmedAddress()
            {
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                var user = new User
                {
                    EmailAddress = null,
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                userService.Setup(u => u.ConfirmEmailAddress(user, "the-token")).Returns(true);
                var messageService = new Mock<IMessageService>(MockBehavior.Strict); // will not be called
                var controller = CreateController(messageService: messageService, userService: userService);

                // act:
                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                // verify:
                Assert.True(model.SuccessfulConfirmation);
                Assert.True(model.ConfirmingNewAccount);
            }

            [Fact]
            public void DoesntSendAccountChangedEmailsIfConfirmationTokenDoesntMatch()
            {
                var userService = new Mock<IUserService>(MockBehavior.Strict);
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                userService.Setup(u => u.FindByUsername("username")).Returns(user);
                userService.Setup(u => u.ConfirmEmailAddress(user, "faketoken")).Returns(false);
                var messageService = new Mock<IMessageService>(MockBehavior.Strict); // will not be called
                var controller = CreateController(messageService: messageService, userService: userService);

                // act:
                var model = (controller.Confirm("username", "faketoken") as ViewResult).Model as EmailConfirmationModel;

                // verify:
                Assert.False(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
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
                var controller = CreateController(userService: userService);

                var model = (controller.Confirm("username", "not-the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.False(model.SuccessfulConfirmation);
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
                var controller = CreateController(userService: userService);
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
                var controller = CreateController(userService: userService);
                var model = new EditProfileViewModel { EmailAddress = "new@example.com", EmailAllowed = true };

                var result = controller.Edit(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(
                    "Account settings saved! We sent a confirmation email to verify your new email. When you confirm the email address, it will take effect and we will forget the old one.",
                    controller.TempData["Message"]);
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
                messageService.Setup(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>())).Throws(
                    new InvalidOperationException());
                var controller = CreateController(userService: userService, messageService: messageService);
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
                var controller = CreateController(userService: userService);

                var result = controller.Edit(new EditProfileViewModel()) as HttpNotFoundResult;

                Assert.NotNull(result);
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
                const string resetUrl = "https://example.org/?Controller=Users&Action=ResetPassword&username=somebody&token=confirmation";
                messageService.Setup(
                    s => s.SendPasswordResetInstructions(user, resetUrl)
                    );
                var userService = new Mock<IUserService>();
                userService.Setup(s => s.GeneratePasswordResetToken("user", 1440)).Returns(user);
                var controller = CreateController(userService: userService, messageService: messageService);
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
                var controller = CreateController(userService: userService);
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
                var controller = CreateController(userService: userService);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
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
                var controller = CreateController(userService: userService, currentUser: currentUser);

                controller.GenerateApiKey();

                userService.VerifyAll();
            }
        }

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
                var userService = new Mock<IUserService>();
                userService
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User { Username = "theUsername", EmailAddress = "to@example.com" });
                var controller = CreateController(userService: userService);

                controller.Register(
                    new RegisterRequest
                        {
                            Username = "theUsername",
                            Password = "thePassword",
                            EmailAddress = "theEmailAddress",
                        });

                userService.Verify(
                    x => x.Create(
                        "theUsername",
                        "thePassword",
                        "theEmailAddress"));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                var userService = new Mock<IUserService>();
                userService
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new EntityException("aMessage"));
                var controller = CreateController(userService: userService);

                var result = controller.Register(
                    new RegisterRequest
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
                var messageService = new Mock<IMessageService>();
                string sentConfirmationUrl = null;
                MailAddress sentToAddress = null;
                messageService.Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Callback<MailAddress, string>(
                        (to, url) =>
                            {
                                sentToAddress = to;
                                sentConfirmationUrl = url;
                            });
                var userService = new Mock<IUserService>();
                userService
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(
                        new User
                            {
                                Username = "theUsername",
                                EmailAddress = "to@example.com",
                                EmailConfirmationToken = "confirmation"
                            });
                var config = new Mock<IConfiguration>();
                config.Setup(x => x.ConfirmEmailAddresses).Returns(true);
                var controller = CreateController(config: config, userService: userService, messageService: messageService);

                controller.Register(
                    new RegisterRequest
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

        public class TheResetPasswordMethod
        {
            [Fact]
            public void ShowsErrorIfTokenExpired()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd")).Returns(false);
                var controller = CreateController(userService: userService);
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                controller.ResetPassword("user", "token", model);

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                userService.Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public void ResetsPasswordForValidToken()
            {
                var userService = new Mock<IUserService>();
                userService.Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd")).Returns(true);
                var controller = CreateController(userService: userService);
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
                var config = new Mock<IConfiguration>();
                config.Setup(x => x.ConfirmEmailAddresses).Returns(true);
                var controller = CreateController(config: config);

                var result = controller.Thanks() as ViewResult;

                Assert.Empty(result.ViewName);
                Assert.Null(result.Model);
            }

            [Fact]
            public void ShowsConfirmViewWithModelWhenConfirmingEmailAddressIsNotRequired()
            {
                var config = new Mock<IConfiguration>();
                config.Setup(x => x.ConfirmEmailAddresses).Returns(false);
                var controller = CreateController(config: config);

                var result = controller.Thanks() as ViewResult;

                Assert.Equal("Confirm", result.ViewName);
                var model = result.Model as EmailConfirmationModel;
                Assert.True(model.ConfirmingNewAccount);
                Assert.True(model.SuccessfulConfirmation);
            }
        }
    }
}
