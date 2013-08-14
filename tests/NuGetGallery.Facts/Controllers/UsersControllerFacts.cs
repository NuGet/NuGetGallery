using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class UsersControllerFacts
    {
        public class TheAccountAction
        {
            [Fact]
            public void WillGetTheCurrentUserUsingTheRequestIdentityName()
            {
                var controller = new TestableUsersController();
                controller.MockCurrentIdentity
                          .Setup(stub => stub.Name)
                          .Returns("theUsername");
                controller.MockUserService
                          .Setup(s => s.FindByUsername(It.IsAny<string>()))
                          .Returns(new User { Key = 42 });
                
                //act
                controller.Account();

                // verify
                controller.MockUserService
                          .Verify(stub => stub.FindByUsername("theUsername"));
            }

            [Fact]
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(s => s.FindByUsername(It.IsAny<string>()))
                          .Returns(new User { Key = 42 });
                
                // act
                controller.Account();

                // verify
                controller.MockCuratedFeedService
                          .Verify(query => query.GetFeedsForManager(42));
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheUserApiKey()
            {
                var stubApiKey = Guid.NewGuid();
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(s => s.FindByUsername(It.IsAny<string>()))
                          .Returns(new User { Key = 42, ApiKey = stubApiKey });
                
                // act
                var model = ((ViewResult)controller.Account()).Model as AccountViewModel;

                // verify
                Assert.Equal(stubApiKey.ToString(), model.ApiKey);
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(s => s.FindByUsername(It.IsAny<string>()))
                          .Returns(new User { Key = 42 });
                controller.MockCuratedFeedService
                          .Setup(stub => stub.GetFeedsForManager(It.IsAny<int>()))
                          .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });
                
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
                var controller = new TestableUsersController();

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
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);
                
                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.True(model.SuccessfulConfirmation);
            }

            [Fact]
            public void SendsAccountChangedNoticeWhenConfirmingChangedEmail()
            {
                var user = new User
                    {
                        EmailAddress = "old@example.com",
                        UnconfirmedEmailAddress = "new@example.com",
                        EmailConfirmationToken = "the-token"
                    };
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);
                
                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                Assert.True(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                controller.MockMessageService
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com"));
            }

            [Fact]
            public void DoesntSendAccountChangedEmailsWhenNoOldConfirmedAddress()
            {
                var user = new User
                {
                    EmailAddress = null,
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);
                
                // act:
                var model = (controller.Confirm("username", "the-token") as ViewResult).Model as EmailConfirmationModel;

                // verify:
                Assert.True(model.SuccessfulConfirmation);
                Assert.True(model.ConfirmingNewAccount);
                controller.MockMessageService
                          .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                controller.MockMessageService
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void DoesntSendAccountChangedEmailsIfConfirmationTokenDoesntMatch()
            {
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.ConfirmEmailAddress(user, "faketoken"))
                          .Returns(false);
                
                // act:
                var model = (controller.Confirm("username", "faketoken") as ViewResult).Model as EmailConfirmationModel;

                // verify:
                Assert.False(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                controller.MockMessageService
                          .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                controller.MockMessageService
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
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
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.ConfirmEmailAddress(user, "not-the-token"))
                          .Returns(false);
                
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

                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername(It.IsAny<string>()))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.UpdateProfile(user, "test@example.com", false))
                          .Verifiable();
                var model = new EditProfileViewModel { EmailAddress = "test@example.com", EmailAllowed = false };

                var result = controller.Edit(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                controller.MockUserService
                          .Verify(u => u.UpdateProfile(user, "test@example.com", false));
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

                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername(It.IsAny<string>()))
                          .Returns(user);
                controller.MockUserService
                          .Setup(u => u.UpdateProfile(user, "new@example.com", true))
                          .Callback(() => user.EmailConfirmationToken = "token");
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

                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername(It.IsAny<string>()))
                          .Returns(user);
                controller.MockMessageService
                          .Setup(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()))
                          .Throws(new InvalidOperationException());
                var model = new EditProfileViewModel { EmailAddress = "old@example.com", EmailAllowed = true };

                var result = controller.Edit(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal("Account settings saved!", controller.TempData["Message"]);
                controller.MockUserService
                          .Verify(u => u.UpdateProfile(user, It.IsAny<string>(), true));
            }

            [Fact]
            public void WithInvalidUsernameReturnsFileNotFound()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.FindByUsername(It.IsAny<string>()))
                          .ReturnsNull();
                
                var result = controller.Edit(new EditProfileViewModel()) as HttpNotFoundResult;

                Assert.NotNull(result);
            }
        }

        public class TheForgotPasswordMethod
        {
            [Fact]
            public void SendsEmailWithPasswordResetUrl()
            {
                const string resetUrl = "https://example.org/?Controller=Users&Action=ResetPassword&username=somebody&token=confirmation";
                var user = new User
                    {
                        EmailAddress = "some@example.com",
                        Username = "somebody",
                        PasswordResetToken = "confirmation",
                        PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                    };
                var controller = new TestableUsersController();
                controller.MockMessageService
                          .Setup(s => s.SendPasswordResetInstructions(user, resetUrl));
                controller.MockUserService
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns(user);
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                controller.MockMessageService
                          .Verify(s => s.SendPasswordResetInstructions(user, resetUrl));
            }

            [Fact]
            public void RedirectsAfterGeneratingToken()
            {
                var user = new User { EmailAddress = "some@example.com", Username = "somebody" };
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns(user)
                          .Verifiable();
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                controller.MockUserService
                          .Verify(s => s.GeneratePasswordResetToken("user", 1440));
            }

            [Fact]
            public void ReturnsSameViewIfTokenGenerationFails()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns((User)null);
                
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
                var controller = new TestableUsersController();
                controller.MockCurrentIdentity
                          .Setup(i => i.Name)
                          .Returns("the-username");
                
                var result = controller.GenerateApiKey() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal("Account", result.RouteValues["action"]);
                Assert.Equal("Users", result.RouteValues["controller"]);
            }

            [Fact]
            public void GeneratesAnApiKey()
            {
                var controller = new TestableUsersController();
                controller.MockCurrentIdentity
                          .Setup(i => i.Name)
                          .Returns("the-username");
                
                controller.GenerateApiKey();

                controller.MockUserService
                          .Verify(s => s.GenerateApiKey("the-username"));
            }
        }

        public class TheRegisterMethod
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = new TestableUsersController();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.Register(null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void WillCreateTheUser()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(new User { Username = "theUsername", EmailAddress = "to@example.com" });
                
                controller.Register(
                    new RegisterRequest
                        {
                            Username = "theUsername",
                            Password = "thePassword",
                            EmailAddress = "theEmailAddress",
                        });

                controller.MockUserService
                          .Verify(x => x.Create("theUsername", "thePassword", "theEmailAddress"));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                          .Throws(new EntityException("aMessage"));
                
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
                string sentConfirmationUrl = null;
                MailAddress sentToAddress = null;
                var controller = new TestableUsersController();
                controller.MockMessageService
                          .Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                          .Callback<MailAddress, string>((to, url) =>
                          {
                              sentToAddress = to;
                              sentConfirmationUrl = url;
                          });
                controller.MockUserService
                          .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(new User
                          {
                              Username = "theUsername",
                              EmailAddress = "to@example.com",
                              EmailConfirmationToken = "confirmation"
                          });
                controller.MockConfig
                          .Setup(x => x.ConfirmEmailAddresses)
                          .Returns(true);
                
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
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                          .Returns(false);
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                controller.ResetPassword("user", "token", model);

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                controller.MockUserService
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public void ResetsPasswordForValidToken()
            {
                var controller = new TestableUsersController();
                controller.MockUserService
                          .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                          .Returns(true);
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                var result = controller.ResetPassword("user", "token", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                controller.MockUserService
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }
        }

        public class TheThanksMethod
        {
            [Fact]
            public void ShowsDefaultThanksViewWhenConfirmingEmailAddressIsRequired()
            {
                var controller = new TestableUsersController();
                controller.MockConfig
                          .Setup(x => x.ConfirmEmailAddresses)
                          .Returns(true);
                
                var result = controller.Thanks() as ViewResult;

                Assert.Empty(result.ViewName);
                Assert.Null(result.Model);
            }

            [Fact]
            public void ShowsConfirmViewWithModelWhenConfirmingEmailAddressIsNotRequired()
            {
                var controller = new TestableUsersController();
                controller.MockConfig
                          .Setup(x => x.ConfirmEmailAddresses)
                          .Returns(false);
                
                var result = controller.Thanks() as ViewResult;

                Assert.Equal("Confirm", result.ViewName);
                var model = result.Model as EmailConfirmationModel;
                Assert.True(model.ConfirmingNewAccount);
                Assert.True(model.SuccessfulConfirmation);
            }
        }

        public class TestableUsersController : UsersController
        {
            public Mock<ICuratedFeedService> MockCuratedFeedService { get; protected set; }
            public Mock<IPrincipal> MockCurrentUser { get; protected set; }
            public Mock<IIdentity> MockCurrentIdentity { get; protected set; }
            public Mock<IMessageService> MockMessageService { get; protected set; }
            public Mock<IPackageService> MockPackageService { get; protected set; }
            public Mock<IAppConfiguration> MockConfig { get; protected set; }
            public Mock<IUserService> MockUserService { get; protected set; }
            
            public TestableUsersController()
            {
                CuratedFeedService = (MockCuratedFeedService = new Mock<ICuratedFeedService>()).Object;
                CurrentUser = (MockCurrentUser = new Mock<IPrincipal>()).Object;
                MessageService = (MockMessageService = new Mock<IMessageService>()).Object;
                PackageService = (MockPackageService = new Mock<IPackageService>()).Object;
                Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
                UserService = (MockUserService = new Mock<IUserService>()).Object;

                MockCurrentIdentity = new Mock<IIdentity>();
                MockCurrentUser.Setup(u => u.Identity).Returns(MockCurrentIdentity.Object);

                var mockContext = new Mock<HttpContextBase>();
                TestUtility.SetupHttpContextMockForUrlGeneration(mockContext, this);
            }
        }
    }
}
