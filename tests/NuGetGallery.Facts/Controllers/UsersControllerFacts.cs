using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
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
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User { Key = 42 });

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
                controller.SetCurrentUser(new User { Key = 42, ApiKey = stubApiKey });

                // act
                var model = ResultAssert.IsView<AccountViewModel>(controller.Account());

                // verify
                Assert.Equal(stubApiKey.ToString(), model.ApiKey);
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User { Key = 42 });
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(42))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // act
                var model = ResultAssert.IsView<AccountViewModel>(controller.Account());

                // verify
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }

            [Fact]
            public void WillUseApiKeyInColumnIfNoCredentialPresent()
            {
                var apiKey = Guid.NewGuid();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User { Key = 42, ApiKey = apiKey });
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(42))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // act
                var result = controller.Account();

                // verify
                var model = ResultAssert.IsView<AccountViewModel>(result);
                Assert.Equal(apiKey.ToString().ToLowerInvariant(), model.ApiKey);
            }

            [Fact]
            public void WillUseApiKeyInCredentialIfPresent()
            {
                var apiKey = Guid.NewGuid();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User
                {
                    Key = 42,
                    ApiKey = Guid.NewGuid(),
                    Credentials = new List<Credential>() {
                        CredentialBuilder.CreateV1ApiKey(apiKey)
                    }
                });
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(42))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // act
                var result = controller.Account();

                // verify
                var model = ResultAssert.IsView<AccountViewModel>(result);
                Assert.Equal(apiKey.ToString().ToLowerInvariant(), model.ApiKey);
            }
        }

        public class TheEditMethod : TestContainer
        {
            [Fact]
            public void UpdatesEmailAllowedSetting()
            {
                var user = new User("aUsername")
                {
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                GetMock<IUserService>()
                          .Setup(u => u.UpdateProfile(user, false));
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
                var user = new User("somebody")
                {
                    EmailAddress = "some@example.com",
                    PasswordResetToken = "confirmation",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                GetMock<IMessageService>()
                          .Setup(s => s.SendPasswordResetInstructions(user, resetUrl));
                GetMock<IUserService>()
                          .Setup(s => s.FindByEmailAddress("user"))
                          .Returns(user);
                GetMock<AuthenticationService>()
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns(user);
                var controller = GetController<UsersController>();
                var model = new ForgotPasswordViewModel { Email = "user" };

                controller.ForgotPassword(model);

                GetMock<IMessageService>()
                    .Verify(s => s.SendPasswordResetInstructions(user, resetUrl));
            }

            [Fact]
            public void RedirectsAfterGeneratingToken()
            {
                var user = new User { EmailAddress = "some@example.com", Username = "somebody" };
                GetMock<AuthenticationService>()
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns(user)
                          .Verifiable();
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                          .Verify(s => s.GeneratePasswordResetToken("user", 1440));
            }

            [Fact]
            public void ReturnsSameViewIfTokenGenerationFails()
            {
                GetMock<AuthenticationService>()
                          .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                          .Returns((User)null);
                var controller = GetController<UsersController>();

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
                var user = new User { Username = "the-username" };
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
                var result = controller.GenerateApiKey();

                ResultAssert.IsRedirectToRoute(result, new { action = "Account", controller = "Users" });
            }

            [Fact]
            public void PutsNewCredentialInOldField()
            {
                var user = new User("the-username") { ApiKey = Guid.NewGuid() };
                Credential created = null;
                GetMock<AuthenticationService>()
                    .Setup(u => u.ReplaceCredential(user, It.IsAny<Credential>()))
                    .Callback<User, Credential>((_, c) => created = c);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>()))
                    .Returns(user);

                controller.GenerateApiKey();

                Assert.Equal(created.Value, user.ApiKey.ToString().ToLowerInvariant());
            }

            [Fact]
            public void ReplacesTheApiKeyCredential()
            {
                var user = new User("the-username") { ApiKey = Guid.NewGuid() };
                GetMock<AuthenticationService>()
                    .Setup(u => u.ReplaceCredential(
                        user,
                        It.Is<Credential>(c => c.Type == CredentialTypes.ApiKeyV1)))
                    .Verifiable();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
                controller.GenerateApiKey();

                GetMock<AuthenticationService>().VerifyAll();
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

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Throws(new EntityException("msg"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
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

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("theUsername", "password"))
                    .Returns(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Callback(() => user.UpdateEmailAddress("new@example.com", () => "token"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
                var model = new ChangeEmailRequestModel { NewEmail = "new@example.com", Password = "password" };

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

                GetMock<AuthenticationService>()
                          .Setup(u => u.Authenticate("aUsername", "password"))
                          .Returns(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                          .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                          .Callback(() => user.UpdateEmailAddress("old@example.com", () => "new-token"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new ChangeEmailRequestModel { NewEmail = "old@example.com", Password = "password" };

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

                GetMock<AuthenticationService>()
                          .Setup(u => u.Authenticate("aUsername", "password"))
                          .Returns(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                          .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                          .Callback(() => user.UpdateEmailAddress("new@example.com", () => "new-token"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new ChangeEmailRequestModel { NewEmail = "new@example.com", Password = "password" };

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
                controller.SetCurrentUser(user);

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
                controller.SetCurrentUser(user);

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

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .Returns(false);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

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

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .Throws(new EntityException("msg"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

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

                GetMock<IUserService>()
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = controller.Confirm("username", "the-token");
                var model = (ConfirmationViewModel)((ViewResult)result).Model;

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

                GetMock<IUserService>()
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .Returns(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

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

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, "faketoken"))
                    .Returns(false);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

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

                GetMock<IMessageService>()
                    .Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Callback<MailAddress, string>((to, url) =>
                    {
                        sentToAddress = to;
                        sentConfirmationUrl = url;
                    });

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                controller.ConfirmationRequiredPost();

                // We use a catch-all route for unit tests so we can see the parameters 
                // are passed correctly.
                Assert.Equal("https://nuget.local/account/Confirm/theUsername/confirmation", sentConfirmationUrl);
                Assert.Equal("to@example.com", sentToAddress.Address);
            }
        }

        public class TheChangePasswordMethod : TestContainer
        {
            [Fact]
            public void ReturnsViewIfModelStateInvalid()
            {
                // Arrange
                var controller = GetController<UsersController>();
                controller.ModelState.AddModelError("test", "test");
                var inputModel = new PasswordChangeViewModel();

                // Act
                var result = controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<PasswordChangeViewModel>(result);
                Assert.Same(inputModel, outputModel);
            }

            [Fact]
            public void AddsModelErrorIfAuthServiceFails()
            {
                // Arrange
                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword("user", "old", "new"))
                    .Returns(false);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser("user");

                var inputModel = new PasswordChangeViewModel()
                {
                    OldPassword = "old",
                    NewPassword = "new",
                    ConfirmPassword = "new"
                };

                // Act
                var result = controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<PasswordChangeViewModel>(result);
                Assert.Same(inputModel, outputModel);

                var errorMessages = controller
                    .ModelState["OldPassword"]
                    .Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                Assert.Equal(errorMessages, new[] { Strings.CurrentPasswordIncorrect });
            }

            [Fact]
            public void RedirectsToPasswordChangedIfUserServiceSucceeds()
            {
                // Arrange
                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword("user", "old", "new"))
                    .Returns(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser("user");
                var inputModel = new PasswordChangeViewModel()
                {
                    OldPassword = "old",
                    NewPassword = "new",
                    ConfirmPassword = "new"
                };

                // Act
                var result = controller.ChangePassword(inputModel);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new
                {
                    controller = "Users",
                    action = "PasswordChanged"
                });
            }
        }

        public class TheResetPasswordMethod : TestContainer
        {
            [Fact]
            public void ShowsErrorIfTokenExpired()
            {
                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .Returns(false);
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                controller.ResetPassword("user", "token", model);

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                GetMock<AuthenticationService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public void ResetsPasswordForValidToken()
            {
                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .Returns(true);
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                    {
                        ConfirmPassword = "pwd",
                        NewPassword = "newpwd"
                    };

                var result = controller.ResetPassword("user", "token", model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }
        }

        public class TheThanksMethod : TestContainer
        {
            [Fact]
            public void ShowsDefaultThanksView()
            {
                GetMock<IAppConfiguration>()
                    .Setup(x => x.ConfirmEmailAddresses)
                    .Returns(true);
                var controller = GetController<UsersController>();

                var result = controller.Thanks() as ViewResult;

                Assert.Empty(result.ViewName);
                Assert.Null(result.Model);
            }

            [Fact]
            public void ShowsConfirmViewWithModelWhenConfirmingEmailAddressIsNotRequired()
            {
                GetMock<IAppConfiguration>()
                    .Setup(x => x.ConfirmEmailAddresses)
                    .Returns(false);
                var controller = GetController<UsersController>();

                ResultAssert.IsView(controller.Thanks());
            }
        }
    }
}
