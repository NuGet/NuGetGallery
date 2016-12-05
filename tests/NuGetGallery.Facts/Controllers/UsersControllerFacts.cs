// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class UsersControllerFacts
    {
        public static readonly int CredentialKey = 123;

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
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser(
                    "test",
                    credentialBuilder.CreatePasswordCredential("hunter2"),
                    TestCredentialBuilder.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blarg", "Bloog"));
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
            public async Task UpdatesEmailAllowedSetting()
            {
                var user = new User("aUsername")
                {
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailSubscriptionAsync(user, false, true))
                    .Returns(Task.CompletedTask);

                var result = await controller.ChangeEmailSubscription(false, true);

                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<IUserService>().Verify(u => u.ChangeEmailSubscriptionAsync(user, false, true));
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
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddHours(Constants.PasswordResetTokenExpirationHours)
                };
                GetMock<IMessageService>()
                    .Setup(s => s.SendPasswordResetInstructions(user, resetUrl, true));
                GetMock<IUserService>()
                    .Setup(s => s.FindByEmailAddress("user"))
                    .Returns(user);
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
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
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
                    .CompletesWith(user)
                    .Verifiable();
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = await controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                    .Verify(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60));
            }

            [Fact]
            public async Task ReturnsSameViewIfTokenGenerationFails()
            {
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
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

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenModelIsInvalidItIsRetried(bool forgot)
            {
                var controller = GetController<UsersController>();

                controller.ModelState.AddModelError("test", "test");

                var result = await controller.ResetPassword("user", "token", new PasswordResetViewModel(), forgot);

                Assert.NotNull(result);
                Assert.IsType<ViewResult>(result);

                var viewResult = result as ViewResult;
                Assert.Equal(forgot, viewResult.ViewBag.ForgotPassword); 
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
            [InlineData(null)]
            [InlineData(" ")]
            [Theory]
            public async Task WhenEmptyDescriptionProvidedRedirectsToAccountPageWithError(string description)
            {
                // Arrange 
                var user = new User { Username = "the-username" };
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.GenerateApiKey(
                    description: description,
                    scopes: null,
                    expirationInDays: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.ApiKeyDescriptionRequired, controller.TempData["Message"]);
            }

            [InlineData(180, 180)]
            [InlineData(700, 365)]
            [InlineData(-1, 365)]
            [InlineData(0, 365)]
            [InlineData(null, 365)]
            [Theory]
            public async Task WhenExpirationInDaysIsProvidedItsUsed(int? inputExpirationInDays, int expectedExpirationInDays)
            {
                // Arrange 
                var user = new User("the-username");

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var config = GetMock<IAppConfiguration>();
                config.SetupGet(x => x.ExpirationInDaysForApiKeyV1).Returns(365);

                // Act
                await controller.GenerateApiKey(
                    description: "my new api key",
                    scopes: new [] { NuGetScopes.PackageUnlist },
                    subjects: null,
                    expirationInDays: inputExpirationInDays);
                
                // Assert
                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKeyV1);

                Assert.NotNull(apiKey);
                Assert.NotNull(apiKey.Expires);
                Assert.Equal(expectedExpirationInDays, TimeSpan.FromTicks(apiKey.ExpirationTicks.Value).Days);
            }

            public static IEnumerable<object[]> CreatesNewApiKeyCredential_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            "permissions to several scopes, several packages",
                            new[] {NuGetScopes.PackageUnlist, NuGetScopes.PackagePush},
                            new[] {"abc", "def"},
                            new []
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            "permissions to several scopes, all packages",
                            new [] { NuGetScopes.PackageUnlist, NuGetScopes.PackagePush },
                            null,
                            new []
                            {
                                new Scope("*", NuGetScopes.PackageUnlist),
                                new Scope("*", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            "permissions to single scope, all packages",
                            new [] { NuGetScopes.PackageUnlist },
                            null,
                            new []
                            {
                                new Scope("*", NuGetScopes.PackageUnlist)
                            }
                        },
                        new object[]
                        {
                            "permissions to everything",
                            null,
                            null,
                            new []
                            {
                                new Scope("*", NuGetScopes.All)
                            } 
                        },
                        new object[]
                        {
                            "empty subjects are ignored",
                            new [] { NuGetScopes.PackageUnlist },
                            new[] {"abc", "def", string.Empty, null, "   "},
                            new []
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackageUnlist)
                            }
                        }
                    };
                }
            }
                
            [MemberData(nameof(CreatesNewApiKeyCredential_Input))]
            [Theory]
            public async Task CreatesNewApiKeyCredential(string description, string[] scopes, string[] subjects, Scope[] expectedScopes)
            {
                // Arrange 
                var user = new User("the-username");

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                await controller.GenerateApiKey(
                    description: description,
                    scopes: scopes,
                    subjects: subjects,
                    expirationInDays: null);

                // Assert
                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKeyV1);

                Assert.NotNull(apiKey);
                Assert.Equal(description, apiKey.Description);
                Assert.Equal(expectedScopes.Length, apiKey.Scopes.Count);

                foreach (var expectedScope in expectedScopes)
                {
                    var actualScope =
                        apiKey.Scopes.First(x => x.AllowedAction == expectedScope.AllowedAction &&
                                                 x.Subject == expectedScope.Subject);
                    Assert.NotNull(actualScope);
                }
            }

            [Fact]
            public async Task RedirectsToAccountPage()
            {
                var user = new User { Username = "the-username" };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.GenerateApiKey(
                    description: "description",
                    scopes: null,
                    expirationInDays: null);

                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.ApiKeyGenerated, controller.TempData["Message"]);

                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKeyV1);
                Assert.Equal(apiKey.Value, controller.TempData["NewCredentialValue"]);
                Assert.Equal(apiKey.Key, controller.TempData["ModifiedCredentialKey"]);
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

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success,
                                                    new AuthenticatedUser(user, new Credential()));

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .CompletesWith(authResult);
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

                var authResult =
                    new PasswordAuthenticationResult(
                        PasswordAuthenticationResult.AuthenticationResult.Success,
                        new AuthenticatedUser(user, new Credential()));

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("theUsername", "password"))
                    .CompletesWith(authResult);
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

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, new AuthenticatedUser(user, new Credential()));

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("aUsername", "password"))
                    .CompletesWith(authResult);
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
                    .CompletesWith(new PasswordAuthenticationResult(
                        PasswordAuthenticationResult.AuthenticationResult.Success, new AuthenticatedUser(user, new Credential())));
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

            [Fact]
            public async Task WhenPasswordValidationFailsErrorIsReturned()
            {
                // Arrange
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "test@example.com",
                    Credentials = new [] { new Credential(CredentialTypes.Password.V3, "abc") }
                };

                Credential credential;
                GetMock<AuthenticationService>()
                    .Setup(u => u.ValidatePasswordCredential(It.IsAny<IEnumerable<Credential>>(), It.IsAny<string>(), out credential))
                    .Returns(false);
               
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                var result = await controller.ChangeEmail(model);

                Assert.IsType<ViewResult>(result);
                Assert.IsType<AccountViewModel>(((ViewResult) result).Model);
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
                    Credentials = new List<Credential> {
                        new CredentialBuilder().CreatePasswordCredential("abc")
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
                user.Credentials.Add(new CredentialBuilder().CreatePasswordCredential("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new", false))
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
                user.Credentials.Add(new CredentialBuilder().CreatePasswordCredential("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new", false))
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
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test");
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
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreatePasswordCredential("password"));
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
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.NoCredentialToRemove, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItRemovesCredAndSendsNotificationToUser()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreatePasswordCredential("password");
                var user = fakes.CreateUser("test",
                    cred,
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));

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
                var fakes = Get<Fakes>();
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "bloog");
                var user = fakes.CreateUser("test", cred);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(
                    credentialType: cred.Type,
                    credentialKey: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CannotRemoveOnlyLoginCredential, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenNoCredential_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreatePasswordCredential("password"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(
                    credentialType: CredentialTypes.ExternalPrefix + "MicrosoftAccount",
                    credentialKey: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.NoCredentialToRemove, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItRemovesCredAndSendsNotificationToUser()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog");
                var user = fakes.CreateUser("test",
                    cred,
                    credentialBuilder.CreatePasswordCredential("password"));

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
                var result = await controller.RemoveCredential(
                    credentialType: cred.Type,
                    credentialKey: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<AuthenticationService>().VerifyAll();
                GetMock<IMessageService>().VerifyAll();
            }
        }

        public class TheRegenerateCredentialAction : TestContainer
        {
            [Fact]
            public async Task GivenNoCredential_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();

                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1)));
                var cred = user.Credentials.First();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(1, user.Credentials.Count);
                Assert.True(user.Credentials.Contains(cred));
            }

            [Fact]
            public async Task GivenANonApiKeyCredential_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var controller = GetController<UsersController>();

                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: "not api key",
                    credentialKey: CredentialKey);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
            }

            [Fact]
            public async Task GivenLegacyApiKey_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var apiKey = new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1));
                var user = fakes.CreateUser("test", apiKey);
                var cred = user.Credentials.First();
                cred.Key = CredentialKey;

                var authenticationService = GetMock<AuthenticationService>();
                authenticationService
                    .Setup(x => x.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                
                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                authenticationService.Verify(x => x.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Never);
            }

            public static IEnumerable<object[]> RegenerateApiKeyCredential_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            "permissions to several scopes, several packages",
                            new []
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            "permissions to everything",
                            new []
                            {
                                new Scope(null, NuGetScopes.All)
                            }
                        }
                    };
                }
            }

            [MemberData(nameof(RegenerateApiKeyCredential_Input))]
            [Theory]
            public async Task GivenValidRequest_ItGeneratesNewCredAndRemovesOldCredAndSendsNotificationToUser(
                string description, Scope[] scopes)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var apiKey = new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1));
                apiKey.Description = description;
                apiKey.Scopes = scopes;
                apiKey.Expires -= TimeSpan.FromDays(1);

                var user = fakes.CreateUser("test", apiKey);
                var cred = user.Credentials.First();
                cred.Key = CredentialKey;

                GetMock<AuthenticationService>()
                    .Setup(u => u.AddCredential(
                        user,
                        It.Is<Credential>(c => c.Type == CredentialTypes.ApiKeyV1)))
                    .Callback<User, Credential>((u, c) => u.Credentials.Add(c))
                    .Completes()
                    .Verifiable();

                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                     .Callback<User, Credential>((u, c) => u.Credentials.Remove(c))
                    .Completes()
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.ApiKeyGenerated, controller.TempData["Message"]);
                GetMock<AuthenticationService>().VerifyAll();

                var newApiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKeyV1);

                Assert.NotNull(newApiKey);
                Assert.Equal(newApiKey.Value, controller.TempData["NewCredentialValue"]);
                Assert.Equal(newApiKey.Key, controller.TempData["ModifiedCredentialKey"]);
               
                Assert.Equal(description, newApiKey.Description);
                Assert.Equal(scopes.Length, newApiKey.Scopes.Count);
                Assert.True(newApiKey.Expires > DateTime.UtcNow);

                foreach (var expectedScope in scopes)
                {
                    var actualScope =
                        newApiKey.Scopes.First(x => x.AllowedAction == expectedScope.AllowedAction &&
                                                 x.Subject == expectedScope.Subject);
                    Assert.NotNull(actualScope);
                }
            }
        }

        public class TheExpireCredentialAction : TestContainer
        {
            [Fact]
            public async Task GivenNoCredential_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1)));
                var cred = user.Credentials.First();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.ExpireCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItExpiresCred()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1)));
                var cred = user.Credentials.First();
                cred.Key = CredentialKey;

                GetMock<AuthenticationService>()
                    .Setup(a => a.ExpireCredential(user, cred))
                    .Completes()
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.ExpireCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CredentialExpired, controller.TempData["Message"]);
                Assert.Equal(cred.Key, controller.TempData["ModifiedCredentialKey"]);
                GetMock<AuthenticationService>().VerifyAll();
            }
        }
    }
}

