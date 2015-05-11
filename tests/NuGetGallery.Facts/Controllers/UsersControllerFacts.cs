// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Xunit;
using Xunit.Extensions;

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
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User { Key = 42 });
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(42))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // act
                var model = ResultAssert.IsView<AccountViewModel>(controller.Account(), viewName: "Account");

                // verify
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }

            [Fact]
            public void LoadsDescriptionsOfCredentialsInToViewModel()
            {
                // Arrange
                var user = Fakes.CreateUser(
                    "test",
                    CredentialBuilder.CreatePbkdf2Password("hunter2"),
                    CredentialBuilder.CreateV1ApiKey(Guid.NewGuid()),
                    CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blarg", "Bloog"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.Account();

                // Assert
                var model = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                var descs = model.Credentials.ToDictionary(c => c.Kind); // Should only be one of each kind
                Assert.Equal(3, descs.Count);
                Assert.Equal(Strings.CredentialType_Password, descs[CredentialKind.Password].TypeCaption);
                Assert.Equal(Strings.CredentialType_ApiKey, descs[CredentialKind.Token].TypeCaption);
                Assert.Equal(Strings.MicrosoftAccount_Caption, descs[CredentialKind.External].TypeCaption);
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
                Assert.Equal("https://nuget.local/account/confirm/theUsername/confirmation", sentConfirmationUrl);
                Assert.Equal("to@example.com", sentToAddress.Address);
            }
        }

        public class TheChangeEmailSubscriptionAction : TestContainer
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
                    .Setup(u => u.ChangeEmailSubscription(user, false));
                
                var result = controller.ChangeEmailSubscription(false);

                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<IUserService>().Verify(u => u.ChangeEmailSubscription(user, false));
            }
        }

        public class TheThanksAction : TestContainer
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

        public class TheForgotPasswordAction : TestContainer
        {
            [Fact]
            public async Task SendsEmailWithPasswordResetUrl()
            {
                const string resetUrl = "https://nuget.local/account/forgotpassword/somebody/confirmation";
                var user = new User("somebody")
                {
                    EmailAddress = "some@example.com",
                    PasswordResetToken = "confirmation",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddDays(1)
                };
                GetMock<IMessageService>()
                    .Setup(s => s.SendPasswordResetInstructions(user, resetUrl, true));
                GetMock<IUserService>()
                    .Setup(s => s.FindByEmailAddress("user"))
                    .Returns(user);
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                    .CompletesWith(user);
                var controller = GetController<UsersController>();
                var model = new ForgotPasswordViewModel { Email = "user" };

                await controller.ForgotPassword(model);

                GetMock<IMessageService>()
                    .Verify(s => s.SendPasswordResetInstructions(user, resetUrl, true));
            }

            [Fact]
            public async Task RedirectsAfterGeneratingToken()
            {
                var user = new User { EmailAddress = "some@example.com", Username = "somebody" };
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                    .CompletesWith(user)
                    .Verifiable();
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = await controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                    .Verify(s => s.GeneratePasswordResetToken("user", 1440));
            }

            [Fact]
            public async Task ReturnsSameViewIfTokenGenerationFails()
            {
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", 1440))
                    .CompletesWithNull();
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = await controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
            }
        }

        public class TheResetPasswordAction : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ShowsErrorIfTokenExpired(bool forgot)
            {
                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .CompletesWithNull();
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                await controller.ResetPassword("user", "token", model, forgot);

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                GetMock<AuthenticationService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ResetsPasswordForValidToken(bool forgot)
            {
                var cred = new Credential("foo", "bar") { User = new User("foobar") };
                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .CompletesWith(cred);
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                var result = await controller.ResetPassword("user", "token", model, forgot) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public async Task SendsPasswordAddedMessageWhenForgotFalse()
            {
                var cred = new Credential("foo", "bar") { User = new User("foobar") };
                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .CompletesWith(cred);
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                await controller.ResetPassword("user", "token", model, forgot: false);

                GetMock<IMessageService>()
                    .Verify(m => m.SendCredentialAddedNotice(cred.User, cred));
            }
        }

        public class TheConfirmAction : TestContainer
        {
            [Fact]
            public async Task ConfirmsTheUser()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Have to set this up first because it needs to return a task.
                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, "aToken"))
                    .CompletesWith(true);

                var result = await controller.Confirm("aUsername", "aToken");

                GetMock<IUserService>()
                    .Verify(u => u.ConfirmEmailAddress(user, "aToken"));
            }

            [Fact]
            public async Task ShowsAnErrorForWrongUsername()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("wrongUsername", "aToken");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.WrongUsername);
            }

            [Fact]
            public async Task ShowsAnErrorForWrongToken()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .CompletesWith(false);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("aUsername", "wrongToken");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
            }

            [Fact]
            public async Task ShowsAnErrorForConflictingEmailAddress()
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

                var result = await controller.Confirm("aUsername", "aToken");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.DuplicateEmailAddress);
            }

            [Fact]
            public async Task SendsAccountChangedNoticeWhenConfirmingChangedEmail()
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
                          .CompletesWith(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("username", "the-token");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com"));
            }

            [Fact]
            public async Task DoesntSendAccountChangedEmailsWhenNoOldConfirmedAddress()
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
                          .CompletesWith(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // act:
                var result = await controller.Confirm("username", "the-token");
                
                // verify:
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SuccessfulConfirmation);
                Assert.True(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task DoesntSendAccountChangedEmailsIfConfirmationTokenDoesntMatch()
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
                    .CompletesWith(false);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // act:
                var result = await controller.Confirm("username", "faketoken");
                
                // verify:
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                GetMock<IMessageService>()
                  .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }
        }

        public class TheGenerateApiKeyAction : TestContainer
        {
            [Fact]
            public async Task RedirectsToAccountPage()
            {
                var user = new User { Username = "the-username" };
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
                var result = await controller.GenerateApiKey();

                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
            }

            [Fact]
            public async Task ReplacesTheApiKeyCredential()
            {
                var user = new User("the-username");
                GetMock<AuthenticationService>()
                    .Setup(u => u.ReplaceCredential(
                        user,
                        It.Is<Credential>(c => c.Type == CredentialTypes.ApiKeyV1)))
                    .Completes()
                    .Verifiable();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                await controller.GenerateApiKey();

                GetMock<AuthenticationService>().VerifyAll();
            }
        }

        public class TheChangeEmailAction : TestContainer
        {
            [Fact]
            public async Task DoesNotLetYouUseSomeoneElsesConfirmedEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "old@example.com",
                    Key = 1,
                };

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .CompletesWith(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Throws(new EntityException("msg"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.ChangeEmail(
                    new AccountViewModel()
                    {
                        ChangeEmail = new ChangeEmailViewModel
                        {
                            NewEmail = "new@example.com"
                        }
                    });
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("msg", controller.ModelState["ChangeEmail.NewEmail"].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task SendsEmailChangeConfirmationNoticeWhenChangingAConfirmedEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("theUsername", "password"))
                    .CompletesWith(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Callback(() => user.UpdateEmailAddress("new@example.com", () => "token"))
                    .Completes();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel()
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                var result = await controller.ChangeEmail(model);

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()));
            }

            [Fact]
            public async Task DoesNotSendEmailChangeConfirmationNoticeWhenAddressDoesntChange()
            {
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    Username = "aUsername",
                };

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("aUsername", "password"))
                    .CompletesWith(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                    .Callback(() => user.UpdateEmailAddress("old@example.com", () => "new-token"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel()
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "old@example.com",
                        Password = "password"
                    }
                };

                await controller.ChangeEmail(model);

                GetMock<IUserService>()
                    .Verify(u => u.ChangeEmailAddress(user, "old@example.com"), Times.Never());
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task DoesNotSendEmailChangeConfirmationNoticeWhenUserWasNotConfirmed()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                };

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("aUsername", "password"))
                    .CompletesWith(new AuthenticatedUser(user, new Credential()));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                    .Callback(() => user.UpdateEmailAddress("new@example.com", () => "new-token"))
                    .Completes();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel()
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                await controller.ChangeEmail(model);

                Assert.Equal("Your new email address was saved!", controller.TempData["Message"]);
                GetMock<IUserService>()
                    .Verify(u => u.ChangeEmailAddress(user, "new@example.com"));
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
            }
        }

        public class TheChangePasswordAction : TestContainer
        {
            [Fact]
            public async Task GivenInvalidView_ItReturnsView()
            {
                // Arrange
                var controller = GetController<UsersController>();
                controller.ModelState.AddModelError("ChangePassword.blarg", "test");
                var inputModel = new AccountViewModel();
                controller.SetCurrentUser(new User()
                {
                    Credentials = new List<Credential>() {
                        CredentialBuilder.CreatePbkdf2Password("abc")
                    }
                });

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                Assert.Same(inputModel, outputModel);
            }

            [Fact]
            public async Task GivenFailureInAuthService_ItAddsModelError()
            {
                // Arrange
                var user = new User("foo");
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new"))
                    .CompletesWith(false);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var inputModel = new AccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        OldPassword = "old",
                        NewPassword = "new",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                Assert.Same(inputModel, outputModel);

                var errorMessages = controller
                    .ModelState["ChangePassword.OldPassword"]
                    .Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                Assert.Equal(errorMessages, new[] { Strings.CurrentPasswordIncorrect });
            }

            [Fact]
            public async Task GivenSuccessInAuthService_ItRedirectsBackToManageCredentialsWithMessage()
            {
                // Arrange
                var user = new User("foo");
                user.Credentials.Add(CredentialBuilder.CreatePbkdf2Password("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new"))
                    .CompletesWith(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                var inputModel = new AccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        OldPassword = "old",
                        NewPassword = "new",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.PasswordChanged, controller.TempData["Message"]);
            }

            [Fact]
            public async Task GivenNoOldPassword_ItSendsAPasswordSetEmail()
            {
                // Arrange
                var user = Fakes.CreateUser("test");
                user.EmailAddress = "confirmed@example.com";
                
                GetMock<AuthenticationService>()
                    .Setup(a => a.GeneratePasswordResetToken(user, It.IsAny<int>()))
                    .Callback<User, int>((u, _) => u.PasswordResetToken = "t0k3n")
                    .Completes();

                string actualConfirmUrl = null;
                GetMock<IMessageService>()
                    .Setup(a => a.SendPasswordResetInstructions(user, It.IsAny<string>(), false))
                    .Callback<User, string, bool>((_, url, __) => actualConfirmUrl = url)
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                await controller.ChangePassword(new AccountViewModel());

                // Assert
                Assert.Equal("https://nuget.local/account/setpassword/test/t0k3n", actualConfirmUrl);
                GetMock<IMessageService>().VerifyAll();
            }
        }

        public class TheRemovePasswordAction : TestContainer
        {
            [Fact]
            public async Task GivenNoOtherLoginCredentials_ItRedirectsBackWithAnErrorMessage()
            {
                // Arrange
                var user = Fakes.CreateUser("test",
                    CredentialBuilder.CreatePbkdf2Password("password"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CannotRemoveOnlyLoginCredential, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenNoPassword_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var user = Fakes.CreateUser("test",
                    CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItRemovesCredAndSendsNotificationToUser()
            {
                // Arrange
                var cred = CredentialBuilder.CreatePbkdf2Password("password");
                var user = Fakes.CreateUser("test",
                    cred,
                    CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));
                
                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                    .Completes()
                    .Verifiable();
                GetMock<IMessageService>()
                    .Setup(m => m.SendCredentialRemovedNotice(user, cred))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<AuthenticationService>().VerifyAll();
                GetMock<IMessageService>().VerifyAll();
            }
        }

        public class TheRemoveCredentialAction : TestContainer
        {
            [Fact]
            public async Task GivenNoOtherLoginCredentials_ItRedirectsBackWithAnErrorMessage()
            {
                // Arrange
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog");
                var user = Fakes.CreateUser("test", cred);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(cred.Type);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CannotRemoveOnlyLoginCredential, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenNoCredential_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var user = Fakes.CreateUser("test",
                    CredentialBuilder.CreatePbkdf2Password("password"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(CredentialTypes.ExternalPrefix + "MicrosoftAccount");

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItRemovesCredAndSendsNotificationToUser()
            {
                // Arrange
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog");
                var user = Fakes.CreateUser("test",
                    cred,
                    CredentialBuilder.CreatePbkdf2Password("password"));

                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                    .Completes()
                    .Verifiable();
                GetMock<IMessageService>()
                    .Setup(m => m.SendCredentialRemovedNotice(user, cred))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(cred.Type);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<AuthenticationService>().VerifyAll();
                GetMock<IMessageService>().VerifyAll();
            }
        }
    }
}

