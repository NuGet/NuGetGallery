// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Autofac;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class AccountsControllerFacts<TAccountsController, TUser, TAccountViewModel>
        where TUser : User
        where TAccountViewModel : AccountViewModel<TUser>
        where TAccountsController : AccountsController<TUser, TAccountViewModel>
    {
        protected const string AllowedCurrentUsersDataName = "AllowedCurrentUsers_Data";

        public abstract class AccountsControllerTestContainer : TestContainer
        {
            public Fakes Fakes = new Fakes();

            private const string AccountEnvironmentKey = "nuget.account";

            public TAccountsController GetController()
            {
                return GetController<TAccountsController>();
            }

            protected TUser GetAccount(TAccountsController controller)
            {
                var environment = controller.OwinContext.Environment;
                if (!environment.TryGetValue(AccountEnvironmentKey, out var accountObj))
                {
                    TUser account = accountObj as TUser;
                    if (account == null)
                    {
                        account = Fakes.User as TUser ?? Fakes.Organization as TUser;
                        environment[AccountEnvironmentKey] = account;
                        // Remove invalid credential added by Fakes
                        if (account is Organization)
                        {
                            account.Credentials.Clear();
                        }
                    }
                }
                return environment[AccountEnvironmentKey] as TUser;
            }
        }

        public abstract class TheAccountBaseAction : AccountsControllerTestContainer
        {

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public void WillGetCuratedFeedsManagedByTheCurrentUser(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController<TAccountsController>();
                var account = GetAccount(controller);

                // Act
                InvokeAccountInternal(controller, getCurrentUser);

                // Assert
                GetMock<ICuratedFeedService>()
                    .Verify(query => query.GetFeedsForManager(account.Key));
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController<TAccountsController>();
                var account = GetAccount(controller);
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(account.Key))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // Act
                var result = InvokeAccountInternal(controller, getCurrentUser);

                // Assert
                var model = ResultAssert.IsView<TAccountViewModel>(result, viewName: controller.AccountAction);
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }

            protected abstract ActionResult InvokeAccount(TAccountsController controller);

            private ActionResult InvokeAccountInternal(TAccountsController controller, Func<Fakes, User> getCurrentUser)
            {
                var account = GetAccount(controller);
                controller.SetCurrentUser(getCurrentUser(Fakes));
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);

                return InvokeAccount(controller);
            }
        }

        public abstract class TheCancelChangeEmailBaseAction : AccountsControllerTestContainer
        {
            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenAlreadyConfirmed_RedirectsToAccount(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeCancelChangeEmail(controller, account, getCurrentUser);

                // Assert
                GetMock<IUserService>().Verify(u => u.CancelChangeEmailAddress(It.IsAny<User>()), Times.Never);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenUnconfirmed_CancelsEmailChangeRequest(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var result = await InvokeCancelChangeEmail(controller, account, getCurrentUser);

                // Assert
                GetMock<IUserService>().Verify(u => u.CancelChangeEmailAddress(account), Times.Once);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
                Assert.Equal(controller.Messages.EmailUpdateCancelled, controller.TempData["Message"]);
            }

            protected virtual Task<ActionResult> InvokeCancelChangeEmail(
                TAccountsController controller,
                TUser account,
                Func<Fakes, User> getCurrentUser,
                TAccountViewModel model = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                userService.Setup(u => u.CancelChangeEmailAddress(account))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                model = model ?? Activator.CreateInstance<TAccountViewModel>();
                model.AccountName = account.Username;

                // Act
                return controller.CancelChangeEmail(model);
            }
        }

        public abstract class TheChangeEmailBaseAction : AccountsControllerTestContainer
        {
            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenServiceThrowsEntityException_ShowsModelStateError(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "account2@example.com";

                // Act
                var result = await InvokeChangeEmail(controller, account, getCurrentUser, model,
                    exception: new EntityException("e.g., Another user already has that email"));

                // Assert
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(1, controller.ModelState.Keys.Count);
                Assert.Equal("ChangeEmail.NewEmail", controller.ModelState.Keys.Single());
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenNewEmailIsInvalid_DoesNotSaveChanges(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "invalid";
                controller.ModelState.AddModelError("ChangeEmail.NewEmail", "Invalid format");

                // Act
                var result = await InvokeChangeEmail(controller, account, getCurrentUser, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
                Assert.False(controller.ModelState.IsValid);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenNewEmailIsSame_RedirectsWithoutChange(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = account.EmailAddress;

                // Act
                var result = await InvokeChangeEmail(controller, account, getCurrentUser, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual Task WhenNewEmailIsUnconfirmedAndDifferentAndWasConfirmed_SavesChanges(Func<Fakes, User> getCurrentUser)
            {
                return WhenNewEmailIsDifferentAndWasConfirmedHelper(getCurrentUser, newEmailIsConfirmed: false);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual Task WhenNewEmailIsConfirmedAndDifferentAndWasConfirmed_SavesChanges(Func<Fakes, User> getCurrentUser)
            {
                return WhenNewEmailIsDifferentAndWasConfirmedHelper(getCurrentUser, newEmailIsConfirmed: true);
            }

            /// <remarks>
            /// Normally, you should use a single <see cref="TheoryAttribute"/> that enumerates through the possible values of <paramref name="getCurrentUser"/> and <paramref name="newEmailIsConfirmed"/>,
            /// but because we are using test case "inheritance" (search for properties with the same name as <see cref="AllowedCurrentUsersDataName"/>), this is not possible.
            /// </remarks>
            private async Task WhenNewEmailIsDifferentAndWasConfirmedHelper(Func<Fakes, User> getCurrentUser, bool newEmailIsConfirmed)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "account2@example.com";

                // Act
                var result = await InvokeChangeEmail(controller, account, getCurrentUser, model, newEmailIsConfirmed);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Once);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNoticeAsync(It.IsAny<User>(), It.IsAny<string>()),
                    newEmailIsConfirmed ? Times.Never() : Times.Once());
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenNewEmailIsDifferentAndWasUnconfirmed_SavesChanges(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "account2@example.com";

                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var result = await InvokeChangeEmail(controller, account, getCurrentUser, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Once);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNoticeAsync(It.IsAny<User>(), It.IsAny<string>()),
                    Times.Never);
            }

            protected virtual TAccountViewModel CreateViewModel(TUser account)
            {
                var model = Activator.CreateInstance<TAccountViewModel>();
                model.AccountName = account.Username;
                model.ChangeEmail = new ChangeEmailViewModel();
                return model;
            }

            protected virtual Task<ActionResult> InvokeChangeEmail(
                TAccountsController controller,
                TUser account,
                Func<Fakes, User> getCurrentUser,
                TAccountViewModel model = null,
                bool newEmailIsConfirmed = false,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var messageService = GetMock<IMessageService>();
                messageService.Setup(m => m.SendEmailChangeConfirmationNoticeAsync(It.IsAny<User>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);

                var setup = userService.Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                    .Callback<User, string>((acct, newEmail) => 
                    {
                        if (newEmailIsConfirmed)
                        {
                            acct.EmailAddress = newEmail;
                        }
                        else
                        {
                            acct.UnconfirmedEmailAddress = newEmail;
                        }
                    });

                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    setup.Returns(Task.CompletedTask).Verifiable();
                }

                model = model ?? CreateViewModel(account);
                var password = account.Credentials.FirstOrDefault(c => CredentialTypes.IsPassword(c.Type));
                if (password != null)
                {
                    model.ChangeEmail.Password = Fakes.Password;
                }

                // Act
                return controller.ChangeEmail(model);
            }
        }

        public abstract class TheChangeEmailSubscriptionBaseAction : AccountsControllerTestContainer
        {
            public static IEnumerable<object[]> UpdatesEmailPreferences_DefaultData
            {
                get
                {
                    foreach (var emailAllowed in new[] { false, true })
                    {
                        foreach (var notifyPackagePushed in new[] { false, true })
                        {
                            yield return MemberDataHelper.AsData(emailAllowed, notifyPackagePushed);
                        }
                    }
                }
            }

            [Theory]
            [MemberData("UpdatesEmailPreferences_Data")]
            public virtual async Task UpdatesEmailPreferences(Func<Fakes, User> getCurrentUser, bool emailAllowed, bool notifyPackagePushed)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeChangeEmailSubscription(controller, getCurrentUser, emailAllowed, notifyPackagePushed);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
                GetMock<IUserService>().Verify(u => u.ChangeEmailSubscriptionAsync(account, emailAllowed, notifyPackagePushed));
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task DisplaysMessageOnUpdate(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();

                // Act
                var result = await InvokeChangeEmailSubscription(controller, getCurrentUser);

                // Assert
                Assert.Equal(controller.Messages.EmailPreferencesUpdated, controller.TempData["Message"]);
            }

            protected virtual async Task<ActionResult> InvokeChangeEmailSubscription(
                TAccountsController controller,
                Func<Fakes, User> getCurrentUser,
                bool emailAllowed = true,
                bool notifyPackagePushed = true)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var account = GetAccount(controller);
                account.Username = "aUsername";
                account.EmailAddress = "test@example.com";
                account.EmailAllowed = !emailAllowed;
                account.NotifyPackagePushed = !notifyPackagePushed;

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                userService.Setup(u => u.ChangeEmailSubscriptionAsync(account, emailAllowed, notifyPackagePushed))
                    .Returns(Task.CompletedTask);

                var viewModel = Activator.CreateInstance<TAccountViewModel>();
                viewModel.AccountName = account.Username;
                viewModel.ChangeNotifications = new ChangeNotificationsViewModel
                {
                    EmailAllowed = emailAllowed,
                    NotifyPackagePushed = notifyPackagePushed
                };

                // Act
                return await controller.ChangeEmailSubscription(viewModel);
            }
        }

        public abstract class TheConfirmationRequiredBaseAction : AccountsControllerTestContainer
        {
            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual void WhenAccountAlreadyConfirmed(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = InvokeConfirmationRequired(controller, account, getCurrentUser);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.AlreadyConfirmed);
                Assert.Equal(account.EmailAddress, model.ConfirmedEmailAddress);
                Assert.Null(model.UnconfirmedEmailAddress);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual void WhenAccountIsNotConfirmed(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var result = InvokeConfirmationRequired(controller, account, getCurrentUser);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.AlreadyConfirmed);
                Assert.Null(model.ConfirmedEmailAddress);
                Assert.Equal(account.UnconfirmedEmailAddress, model.UnconfirmedEmailAddress);
            }

            protected virtual ActionResult InvokeConfirmationRequired(
                TAccountsController controller,
                TUser account,
                Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);

                // Act
                return controller.ConfirmationRequired(account.Username);
            }
        }

        public abstract class TheConfirmationRequiredPostBaseAction : AccountsControllerTestContainer
        {
            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenAlreadyConfirmed_DoesNotSendEmail(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeConfirmationRequiredPostAsync(controller, account, getCurrentUser);

                // Assert
                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendNewAccountEmailAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SentEmail);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenIsNotConfirmed_SendsEmail(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                account.EmailConfirmationToken = "confirmation";
                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var confirmationUrl = (account is Organization)
                    ? TestUtility.GallerySiteRootHttps + $"organization/{account.Username}/Confirm?token=confirmation"
                    : TestUtility.GallerySiteRootHttps + $"account/confirm/{account.Username}/confirmation";
                var result = await InvokeConfirmationRequiredPostAsync(controller, account, getCurrentUser, confirmationUrl);

                // Assert
                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendNewAccountEmailAsync(It.IsAny<User>(), confirmationUrl), Times.Once);

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SentEmail);
            }

            protected virtual Task<ActionResult> InvokeConfirmationRequiredPostAsync(
                TAccountsController controller,
                TUser account,
                Func<Fakes, User> getCurrentUser,
                string confirmationUrl = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);

                GetMock<IMessageService>()
                    .Setup(m => m.SendNewAccountEmailAsync(
                        account,
                        string.IsNullOrEmpty(confirmationUrl) ? It.IsAny<string>() : confirmationUrl))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                return controller.ConfirmationRequiredPost(account.Username);
            }
        }

        public abstract class TheConfirmBaseAction : AccountsControllerTestContainer
        {
            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WithNullUser_ShowsError(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var accountUsername = "nullUser";
                controller.SetCurrentUser(getCurrentUser(Fakes));

                // Act
                var result = await controller.Confirm(accountUsername, "token");

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.Equal(accountUsername, model.AccountName);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.WrongUsername);
                Assert.True(model.AlreadyConfirmed);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task ClearsReturnUrlFromViewData(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";
                controller.ViewData[Constants.ReturnUrlViewDataKey] = "https://localhost/returnUrl";

                Assert.NotNull(controller.ViewData[Constants.ReturnUrlViewDataKey]);

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser, token);

                // Assert
                Assert.Null(controller.ViewData[Constants.ReturnUrlViewDataKey]);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenAlreadyConfirmed_DoesNotConfirmEmailAddress(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser, token);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.Equal(account.Username, model.AccountName);
                Assert.False(model.SuccessfulConfirmation);

                var userService = GetMock<IUserService>();
                userService.Verify(m => m.ConfirmEmailAddress(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Never);

                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddressAsync(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Never);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenIsNotConfirmedAndNoExistingEmail_ConfirmsEmailAddress(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";

                account.UnconfirmedEmailAddress = "account2@example.com";
                account.EmailAddress = null;

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser, token);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.Equal(account.Username, model.AccountName);
                Assert.True(model.SuccessfulConfirmation);

                var userService = GetMock<IUserService>();
                userService.Verify(m => m.ConfirmEmailAddress(
                    account,
                    It.IsAny<string>()),
                        Times.Once);

                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddressAsync(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Never);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public virtual async Task WhenIsNotConfirmedAndHasExistingEmail_ConfirmsEmailAddress(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";

                account.UnconfirmedEmailAddress = "account2@example.com";

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser, token);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.Equal(account.Username, model.AccountName);
                Assert.True(model.SuccessfulConfirmation);

                var userService = GetMock<IUserService>();
                userService.Verify(m => m.ConfirmEmailAddress(
                    account,
                    It.IsAny<string>()),
                        Times.Once);

                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddressAsync(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Once);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public async Task WhenIsNotConfirmedAndTokenDoesNotMatch_ShowsError(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                account.EmailConfirmationToken = "token";

                account.UnconfirmedEmailAddress = "account2@example.com";

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser, "wrongToken");

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.Equal(account.Username, model.AccountName);
                Assert.False(model.SuccessfulConfirmation);
            }

            [Theory]
            [MemberData(AllowedCurrentUsersDataName)]
            public async Task WhenIsNotConfirmedAndEntityExceptionThrown_ShowsErrorForDuplicateEmail(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";

                account.UnconfirmedEmailAddress = "account2@example.com";

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser, token,
                    exception: new EntityException("msg"));

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.Equal(account.Username, model.AccountName);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.DuplicateEmailAddress);
            }

            protected virtual Task<ActionResult> InvokeConfirm(
                TAccountsController controller,
                TUser account,
                Func<Fakes, User> getCurrentUser,
                string token = "token",
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                var confirmSetup = userService.Setup(u => u.ConfirmEmailAddress(It.IsAny<User>(), It.IsAny<string>()));
                if (exception == null)
                {
                    confirmSetup.Returns(Task.FromResult(token == account.EmailConfirmationToken));
                }
                else
                {
                    confirmSetup.Throws(exception);
                }

                GetMock<IMessageService>()
                    .Setup(m => m.SendEmailChangeNoticeToPreviousEmailAddressAsync(
                        It.IsAny<TUser>(),
                        It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                return controller.Confirm(account.Username, token);
            }
        }
    }
}
