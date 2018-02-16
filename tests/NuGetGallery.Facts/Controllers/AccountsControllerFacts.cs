// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class AccountsControllerFacts<TAccountsController, TUser, TAccountViewModel>
        where TUser : User
        where TAccountViewModel : AccountViewModel
        where TAccountsController : AccountsController<TUser, TAccountViewModel>
    {
        public abstract class AccountsControllerTestContainer : TestContainer
        {
            public Fakes Fakes = new Fakes();

            private const string AccountEnvironmentKey = "nuget.account";

            protected abstract User GetCurrentUser(TAccountsController controller);

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
            [Fact]
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                // Arrange
                var controller = GetController<TAccountsController>();
                var account = GetAccount(controller);
                controller.SetCurrentUser(GetCurrentUser(controller));

                // Act
                InvokeAccountInternal(controller);

                // Assert
                GetMock<ICuratedFeedService>()
                    .Verify(query => query.GetFeedsForManager(account.Key));
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                // Arrange
                var controller = GetController<TAccountsController>();
                var account = GetAccount(controller);
                controller.SetCurrentUser(GetCurrentUser(controller));
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(account.Key))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // Act
                var result = InvokeAccountInternal(controller);

                // Assert
                var model = ResultAssert.IsView<TAccountViewModel>(result, viewName: controller.AccountAction);
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }

            protected abstract ActionResult InvokeAccount(TAccountsController controller);

            private ActionResult InvokeAccountInternal(TAccountsController controller)
            {
                var account = GetAccount(controller);
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);

                return InvokeAccount(controller);
            }
        }

        public abstract class TheCancelChangeEmailBaseAction : AccountsControllerTestContainer
        {
            [Fact]
            public virtual async Task WhenAlreadyConfirmed_RedirectsToAccount()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeCancelChangeEmail(controller, account);

                // Assert
                GetMock<IUserService>().Verify(u => u.CancelChangeEmailAddress(It.IsAny<User>()), Times.Never);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
            }

            [Fact]
            public virtual async Task WhenUnconfirmed_CancelsEmailChangeRequest()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var result = await InvokeCancelChangeEmail(controller, account);

                // Assert
                GetMock<IUserService>().Verify(u => u.CancelChangeEmailAddress(It.IsAny<User>()), Times.Once);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
                Assert.Equal(controller.Messages.EmailUpdateCancelled, controller.TempData["Message"]);
            }

            protected virtual Task<ActionResult> InvokeCancelChangeEmail(
                TAccountsController controller,
                TUser account,
                TAccountViewModel model = null)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);
                userService.Setup(u => u.CancelChangeEmailAddress(It.IsAny<User>()))
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
            [Fact]
            public virtual async Task WhenServiceThrowsEntityException_ShowsModelStateError()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var currentUser = GetCurrentUser(controller);

                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "account2@example.com";

                // Act
                var result = await InvokeChangeEmail(controller, account, model,
                    exception: new EntityException("e.g., Another user already has that email"));

                // Assert
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(1, controller.ModelState.Keys.Count);
                Assert.Equal("ChangeEmail.NewEmail", controller.ModelState.Keys.Single());
            }

            [Fact]
            public virtual async Task WhenNewEmailIsInvalid_DoesNotSaveChanges()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "invalid";
                controller.ModelState.AddModelError("ChangeEmail.NewEmail", "Invalid format");

                // Act
                var result = await InvokeChangeEmail(controller, account, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
                Assert.False(controller.ModelState.IsValid);
            }

            [Fact]
            public virtual async Task WhenNewEmailIsSame_RedirectsWithoutChange()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = account.EmailAddress;

                // Act
                var result = await InvokeChangeEmail(controller, account, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
            }

            [Fact]
            public virtual async Task WhenNewEmailIsDifferentAndWasConfirmed_SavesChanges()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "account2@example.com";

                // Act
                var result = await InvokeChangeEmail(controller, account, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Once);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });

                Assert.Equal(controller.Messages.EmailUpdatedWithConfirmationRequired, controller.TempData["Message"]);

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public virtual async Task WhenNewEmailIsDifferentAndWasUnconfirmed_SavesChanges()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var model = CreateViewModel(account);
                model.ChangeEmail.NewEmail = "account2@example.com";

                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var result = await InvokeChangeEmail(controller, account, model);

                // Assert
                GetMock<IUserService>().Verify(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Once);
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });

                Assert.Equal(controller.Messages.EmailUpdated, controller.TempData["Message"]);

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()),
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
                TAccountViewModel model = null,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

                var messageService = GetMock<IMessageService>();
                messageService.Setup(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Verifiable();

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);

                var setup = userService.Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                    .Callback<User, string>((acct, newEmail) => { acct.UnconfirmedEmailAddress = newEmail; });

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
            [Theory]
            [InlineData(true, true)]
            [InlineData(true, false)]
            [InlineData(false, true)]
            [InlineData(false, false)]
            public virtual async Task UpdatesEmailPreferences(bool emailAllowed, bool notifyPackagePushed)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeChangeEmailSubscription(controller, emailAllowed, notifyPackagePushed);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = controller.AccountAction });
                GetMock<IUserService>().Verify(u => u.ChangeEmailSubscriptionAsync(account, emailAllowed, notifyPackagePushed));
            }

            [Fact]
            public virtual async Task DisplaysMessageOnUpdate()
            {
                // Arrange
                var controller = GetController();

                // Act
                var result = await InvokeChangeEmailSubscription(controller);

                // Assert
                Assert.Equal(controller.Messages.EmailPreferencesUpdated, controller.TempData["Message"]);
            }

            protected virtual async Task<ActionResult> InvokeChangeEmailSubscription(
                TAccountsController controller,
                bool emailAllowed = true,
                bool notifyPackagePushed = true)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

                var account = GetAccount(controller);
                account.Username = "aUsername";
                account.EmailAddress = "test@example.com";
                account.EmailAllowed = !emailAllowed;
                account.NotifyPackagePushed = !notifyPackagePushed;

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
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
            [Fact]
            public virtual void WhenAccountAlreadyConfirmed()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(GetCurrentUser(controller));

                // Act
                var result = InvokeConfirmationRequired(controller, account);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.AlreadyConfirmed);
                Assert.Equal(account.EmailAddress, model.ConfirmedEmailAddress);
                Assert.Null(model.UnconfirmedEmailAddress);
            }

            [Fact]
            public virtual void WhenAccountIsNotConfirmed()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(GetCurrentUser(controller));

                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var result = InvokeConfirmationRequired(controller, account);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.AlreadyConfirmed);
                Assert.Null(model.ConfirmedEmailAddress);
                Assert.Equal(account.UnconfirmedEmailAddress, model.UnconfirmedEmailAddress);
            }

            protected virtual ActionResult InvokeConfirmationRequired(
                TAccountsController controller,
                TUser account)
            {
                // Arrange
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);

                // Act
                return controller.ConfirmationRequired(account.Username);
            }
        }

        public abstract class TheConfirmationRequiredPostBaseAction : AccountsControllerTestContainer
        {
            [Fact]
            public virtual void WhenAlreadyConfirmed_DoesNotSendEmail()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(GetCurrentUser(controller));

                // Act
                var result = InvokeConfirmationRequiredPost(controller, account);

                // Assert
                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never);

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SentEmail);
            }

            [Fact]
            public virtual void WhenIsNotConfirmed_SendsEmail()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(GetCurrentUser(controller));

                account.EmailConfirmationToken = "confirmation";
                account.UnconfirmedEmailAddress = account.EmailAddress;
                account.EmailAddress = null;

                // Act
                var confirmationUrl = (account is Organization)
                    ? TestUtility.GallerySiteRootHttps + $"organization/{account.Username}/Confirm?token=confirmation"
                    : TestUtility.GallerySiteRootHttps + $"account/confirm/{account.Username}/confirmation";
                var result = InvokeConfirmationRequiredPost(controller, account, confirmationUrl);

                // Assert
                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), confirmationUrl), Times.Once);

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SentEmail);
            }

            protected virtual ActionResult InvokeConfirmationRequiredPost(
                TAccountsController controller,
                TUser account,
                string confirmationUrl = null)
            {
                // Arrange
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);

                GetMock<IMessageService>()
                    .Setup(m => m.SendNewAccountEmail(
                        It.IsAny<MailAddress>(),
                        string.IsNullOrEmpty(confirmationUrl) ? It.IsAny<string>() : confirmationUrl))
                    .Callback<MailAddress, string>((actualMailAddress, actualConfirmationUrl) =>
                    {
                        Assert.Equal(account.UnconfirmedEmailAddress, actualMailAddress.Address);
                    })
                    .Verifiable();

                // Act
                return controller.ConfirmationRequiredPost(account.Username);
            }
        }

        public abstract class TheConfirmBaseAction : AccountsControllerTestContainer
        {
            [Fact]
            public virtual async Task ClearsReturnUrlFromViewData()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";
                controller.SetCurrentUser(GetCurrentUser(controller));
                controller.ViewData[Constants.ReturnUrlViewDataKey] = "https://localhost/returnUrl";

                Assert.NotNull(controller.ViewData[Constants.ReturnUrlViewDataKey]);

                // Act
                var result = await InvokeConfirm(controller, account, token);

                // Assert
                Assert.Null(controller.ViewData[Constants.ReturnUrlViewDataKey]);
            }

            [Fact]
            public virtual async Task WhenAlreadyConfirmed_DoesNotConfirmEmailAddress()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";
                controller.SetCurrentUser(GetCurrentUser(controller));

                // Act
                var result = await InvokeConfirm(controller, account, token);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);

                var userService = GetMock<IUserService>();
                userService.Verify(m => m.ConfirmEmailAddress(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Never);

                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Never);
            }

            [Fact]
            public virtual async Task WhenIsNotConfirmedAndNoExistingEmail_ConfirmsEmailAddress()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";
                controller.SetCurrentUser(GetCurrentUser(controller));

                account.UnconfirmedEmailAddress = "account2@example.com";
                account.EmailAddress = null;

                // Act
                var result = await InvokeConfirm(controller, account, token);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SuccessfulConfirmation);

                var userService = GetMock<IUserService>();
                userService.Verify(m => m.ConfirmEmailAddress(
                    account,
                    It.IsAny<string>()),
                        Times.Once);

                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Never);
            }

            [Fact]
            public virtual async Task WhenIsNotConfirmedAndHasExistingEmail_ConfirmsEmailAddress()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";
                controller.SetCurrentUser(GetCurrentUser(controller));

                account.UnconfirmedEmailAddress = "account2@example.com";

                // Act
                var result = await InvokeConfirm(controller, account, token);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SuccessfulConfirmation);

                var userService = GetMock<IUserService>();
                userService.Verify(m => m.ConfirmEmailAddress(
                    account,
                    It.IsAny<string>()),
                        Times.Once);

                var mailService = GetMock<IMessageService>();
                mailService.Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(
                    It.IsAny<TUser>(),
                    It.IsAny<string>()),
                        Times.Once);
            }

            [Fact]
            public async Task WhenIsNotConfirmedAndTokenDoesNotMatch_ShowsError()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                account.EmailConfirmationToken = "token";
                controller.SetCurrentUser(GetCurrentUser(controller));

                account.UnconfirmedEmailAddress = "account2@example.com";

                // Act
                var result = await InvokeConfirm(controller, account, "wrongToken");

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
            }

            [Fact]
            public async Task WhenIsNotConfirmedAndEntityExceptionThrown_ShowsErrorForDuplicateEmail()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var token = account.EmailConfirmationToken = "token";
                controller.SetCurrentUser(GetCurrentUser(controller));

                account.UnconfirmedEmailAddress = "account2@example.com";

                // Act
                var result = await InvokeConfirm(controller, account, token,
                    exception: new EntityException("msg"));

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.DuplicateEmailAddress);
            }

            protected virtual Task<ActionResult> InvokeConfirm(
                TAccountsController controller,
                TUser account,
                string token = "token",
                EntityException exception = null)
            {
                // Arrange
                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
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
                    .Setup(m => m.SendEmailChangeNoticeToPreviousEmailAddress(
                        It.IsAny<TUser>(),
                        It.IsAny<string>()))
                    .Verifiable();

                // Act
                return controller.Confirm(account.Username, token);
            }
        }
    }
}
