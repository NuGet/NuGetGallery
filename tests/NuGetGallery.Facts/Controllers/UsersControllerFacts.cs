using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class UsersControllerFacts
    {
        public class TheAccountAction : TestContainer
        {
            [Fact]
            public void WillGetTheCurrentUserUsingTheRequestIdentityName()
            {
                var controller = GetController<UsersController>();
                var user = new User { Username = "theUsername" };
                controller.SetUser(user);
                GetMock<IUserService>()
                          .Setup(s => s.FindByUsername(It.IsAny<string>()))
                          .Returns(user);
                
                //act
                controller.Account();

                // verify
                GetMock<IUserService>()
                          .Verify(stub => stub.FindByUsername("theUsername"));
            }

            [Fact]
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                var controller = GetController<UsersController>();
                var user = new User { Username = "theUsername", Key = 42 };
                controller.SetUser(user);
                GetMock<IUserService>()
                          .Setup(s => s.FindByUsername(It.IsAny<string>()))
                          .Returns(user);
                
                // act
                controller.Account();

                // verify
                GetMock<ICuratedFeedService>()
                    .Verify(query => query.GetFeedsForManager(42));
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheUserApiKey()
            {
                var controller = GetController<UsersController>();
                var stubApiKey = Guid.NewGuid();
                var user = new User { Username = "aUser", Key = 42, ApiKey = stubApiKey };
                GetMock<IUserService>()
                    .Setup(s => s.FindByUsername(It.IsAny<string>()))
                    .Returns(user);
                controller.SetUser(user);

                // act
                var model = ((ViewResult)controller.Account()).Model as AccountViewModel;

                // verify
                Assert.Equal(stubApiKey.ToString(), model.ApiKey);
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                var user = new User { Key = 42, Username = "aUsername" };
                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                    .Setup(s => s.FindByUsername(It.IsAny<string>()))
                    .Returns(user);
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(It.IsAny<int>()))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });
                controller.SetUser(user);

                // act
                var model = ((ViewResult)controller.Account()).Model as AccountViewModel;

                // verify
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }
        }

        public class TheEditMethod : TestContainer
        {
            [Fact]
            public void UpdatesEmailAllowedSetting()
            {
                var user = new User
                {
                    Username = "aUsername",
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                          .Setup(u => u.FindByUsername(It.IsAny<string>()))
                          .Returns(user);
                GetMock<IUserService>()
                          .Setup(u => u.UpdateProfile(user, false));
                controller.SetUser(user);
                var model = new EditProfileViewModel { EmailAddress = "test@example.com", EmailAllowed = false };

                var result = controller.Edit(model);

                var viewModel = ResultAssert.IsView<EditProfileViewModel>(result);
                Assert.Same(model, viewModel);
                GetMock<IUserService>().Verify(u => u.UpdateProfile(user, false));
            }
        }

        public class TheForgotPasswordMethod : TestContainer
        {
            [Fact]
            public void SendsEmailWithPasswordResetUrl()
            {
                const string resetUrl = "https://nuget.local/account/ResetPassword/somebody/confirmation";
                var user = new User
                    {
                        EmailAddress = "some@example.com",
                        Username = "somebody",
                        PasswordResetToken = "confirmation",
                        PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                    };
                var controller = GetController<UsersController>();
                GetMock<IMessageService>()
                          .Setup(s => s.SendPasswordResetInstructions(user, resetUrl));
                GetMock<IUserService>()
                          .Setup(s => s.FindByEmailAddress(It.IsAny<string>()))
                          .Returns(user);
                GetMock<IUserService>()
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns(user);
                var model = new ForgotPasswordViewModel { Email = "user" };

                controller.ForgotPassword(model);

                GetMock<IMessageService>()
                    .Verify(s => s.SendPasswordResetInstructions(user, resetUrl));
            }

            [Fact]
            public void RedirectsAfterGeneratingToken()
            {
                var user = new User { EmailAddress = "some@example.com", Username = "somebody" };
                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns(user)
                          .Verifiable();
                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<IUserService>()
                          .Verify(s => s.GeneratePasswordResetToken("user", 1440));
            }

            [Fact]
            public void ReturnsSameViewIfTokenGenerationFails()
            {
                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns((User)null);
                
                var model = new ForgotPasswordViewModel { Email = "user" };
                
                var result = controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
            }
        }

        public class TheGenerateApiKeyMethod : TestContainer
        {
            [Fact]
            public void RedirectsToAccountPage()
            {
                var controller = GetController<UsersController>();
                var user = new User { Username = "the-username" };
                controller.SetUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>()))
                    .Returns(user);
                var result = controller.GenerateApiKey();

                ResultAssert.IsRedirectToRoute(result, new { Controller = "Users", Action = "Account" });
            }

            [Fact]
            public void GeneratesAnApiKey()
            {
                var controller = GetController<UsersController>();
                var user = new User { Username = "the-username" };
                controller.SetUser(user);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>()))
                    .Returns(user);

                controller.GenerateApiKey();

                GetMock<IUserService>()
                          .Verify(s => s.GenerateApiKey("the-username"));
            }
        }

        public class TheChangeEmailAction : TestContainer
        {
            [Fact]
            public void DoesNotLetYouUseSomeoneElsesConfirmedEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "old@example.com",
                    Key = 1,
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(user);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Throws(new EntityException("msg"));

                var result = controller.ChangeEmail(new ChangeEmailRequestModel { NewEmail = "new@example.com" });
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("msg", controller.ModelState["NewEmail"].Errors[0].ErrorMessage);
            }

            [Fact]
            public void SendsEmailChangeConfirmationNoticeWhenChangingAConfirmedEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(user);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Callback(() => user.UpdateEmailAddress("new@example.com", () => "token"));
                var model = new ChangeEmailRequestModel { NewEmail = "new@example.com" };

                var result = controller.ChangeEmail(model);

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()));
            }

            [Fact]
            public void DoesNotSendEmailChangeConfirmationNoticeWhenAddressDoesntChange()
            {
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    Username = "aUsername",
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>()
                          .Setup(u => u.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(user);
                GetMock<IUserService>()
                          .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                          .Callback(() => user.UpdateEmailAddress("old@example.com", () => "new-token"));

                var model = new ChangeEmailRequestModel { NewEmail = "old@example.com" };

                controller.ChangeEmail(model);

                GetMock<IUserService>()
                    .Verify(u => u.ChangeEmailAddress(user, "old@example.com"), Times.Never());
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void DoesNotSendEmailChangeConfirmationNoticeWhenUserWasNotConfirmed()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>()
                          .Setup(u => u.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(user);
                GetMock<IUserService>()
                          .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                          .Callback(() => user.UpdateEmailAddress("new@example.com", () => "new-token"));
                var model = new ChangeEmailRequestModel{ NewEmail = "new@example.com" };

                controller.ChangeEmail(model);

                Assert.Equal("Your new email address was saved!", controller.TempData["Message"]);
                GetMock<IUserService>()
                    .Verify(u => u.ChangeEmailAddress(user, "new@example.com"));
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
            }
        }

        public class TheConfirmAction : TestContainer
        {
            [Fact]
            public void ConfirmsTheUser()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>().Setup(u => u.FindByUsername("aUsername")).Returns(user);

                var result = controller.Confirm("aUsername", "aToken");

                GetMock<IUserService>().Verify(u => u.ConfirmEmailAddress(user, "aToken"));
            }

            [Fact]
            public void ShowsAnErrorForWrongUsername()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>().Setup(u => u.FindByUsername("aUsername")).Returns(user);

                var result = controller.Confirm("wrongUsername", "aToken");
                var model = (ConfirmationViewModel)((ViewResult)result).Model;

                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.WrongUsername);
            }

            [Fact]
            public void ShowsAnErrorForWrongToken()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername("aUsername"))
                    .Returns(user);
                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .Returns(false);

                var result = controller.Confirm("aUsername", "wrongToken");
                var model = (ConfirmationViewModel)((ViewResult)result).Model;

                Assert.False(model.SuccessfulConfirmation);
            }

            [Fact]
            public void ShowsAnErrorForConflictingEmailAddress()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername("aUsername"))
                    .Returns(user);
                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .Throws(new EntityException("msg"));

                var result = controller.Confirm("aUsername", "aToken");
                var model = (ConfirmationViewModel)((ViewResult)result).Model;

                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.DuplicateEmailAddress);
            }

            [Fact]
            public void SendsAccountChangedNoticeWhenConfirmingChangedEmail()
            {
                var user = new User
                {
                    Username = "username",
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var controller = GetController<UsersController>();
                controller.SetUser(user);

                GetMock<IUserService>()
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                GetMock<IUserService>()
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);

                var result = controller.Confirm("username", "the-token");
                var model =  (ConfirmationViewModel)((ViewResult)result).Model;

                Assert.True(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com"));
            }

            [Fact]
            public void DoesntSendAccountChangedEmailsWhenNoOldConfirmedAddress()
            {
                var user = new User
                {
                    Username = "username",
                    EmailAddress = null,
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                          .Setup(u => u.FindByUsername("username"))
                          .Returns(user);
                GetMock<IUserService>()
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);
                controller.SetUser(user);

                // act:
                var result = controller.Confirm("username", "the-token");
                var model = (ConfirmationViewModel)((ViewResult)result).Model;

                // verify:
                Assert.True(model.SuccessfulConfirmation);
                Assert.True(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void DoesntSendAccountChangedEmailsIfConfirmationTokenDoesntMatch()
            {
                var user = new User
                {
                    Username = "username",
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };
                var controller = GetController<UsersController>();
                controller.SetUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>()))
                    .Returns(user);
                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, "faketoken"))
                    .Returns(false);

                // act:
                var model = (controller.Confirm("username", "faketoken") as ViewResult).Model as ConfirmationViewModel;

                // verify:
                Assert.False(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                GetMock<IMessageService>()
                  .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }
        }

        public class TheConfirmationRequiredAction : TestContainer
        {
            [Fact]
            public void WillSendNewUserEmailWhenPosted()
            {
                var user = new User
                {
                    Username = "theUsername",
                    UnconfirmedEmailAddress = "to@example.com",
                    EmailConfirmationToken = "confirmation"
                };

                string sentConfirmationUrl = null;
                MailAddress sentToAddress = null;

                var controller = GetController<UsersController>();
                controller.SetUser(user);

                GetMock<IUserService>()
                    .Setup(x => x.FindByUsername(It.IsAny<string>()))
                    .Returns(user);
                GetMock<IUserService>()
                    .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(user);
                GetMock<IMessageService>()
                    .Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Callback<MailAddress, string>((to, url) =>
                    {
                        sentToAddress = to;
                        sentConfirmationUrl = url;
                    });

                controller.ConfirmationRequiredPost();

                // We use a catch-all route for unit tests so we can see the parameters 
                // are passed correctly.
                Assert.Equal("https://nuget.local/account/Confirm/theUsername/confirmation", sentConfirmationUrl);
                Assert.Equal("to@example.com", sentToAddress.Address);
            }
        }

        public class TheResetPasswordMethod : TestContainer
        {
            [Fact]
            public void ShowsErrorIfTokenExpired()
            {
                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                          .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                          .Returns(false);
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                controller.ResetPassword("user", "token", model);

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                GetMock<IUserService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public void ResetsPasswordForValidToken()
            {
                var controller = GetController<UsersController>();
                GetMock<IUserService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .Returns(true);
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                var result = controller.ResetPassword("user", "token", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<IUserService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }
        }

        public class TheThanksMethod : TestContainer
        {
            [Fact]
            public void ShowsDefaultThanksView()
            {
                var controller = GetController<UsersController>();
                GetMock<IAppConfiguration>()
                          .Setup(x => x.ConfirmEmailAddresses)
                          .Returns(true);
                
                var result = controller.Thanks() as ViewResult;

                Assert.Empty(result.ViewName);
                Assert.Null(result.Model);
            }
        }
    }
}

