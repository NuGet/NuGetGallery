// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public abstract class AccountsController<TUser, TAccountViewModel> : AppController
        where TUser : User
        where TAccountViewModel : AccountViewModel
    {
        public class ViewMessages
        {
            public string EmailConfirmed { get; set; }

            public string EmailPreferencesUpdated { get; set; }

            public string EmailUpdateCancelled { get; set; }

            public string EmailUpdated { get; set; }

            public string EmailUpdatedWithConfirmationRequired { get; set; }
        }

        public AuthenticationService AuthenticationService { get; }

        public ICuratedFeedService CuratedFeedService { get; }

        public IMessageService MessageService { get; }

        public IUserService UserService { get; }

        public AccountsController(
            AuthenticationService authenticationService,
            ICuratedFeedService curatedFeedService,
            IMessageService messageService,
            IUserService userService)
        {
            AuthenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            CuratedFeedService = curatedFeedService ?? throw new ArgumentNullException(nameof(curatedFeedService));
            MessageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            UserService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        public abstract string AccountAction { get; }

        protected internal abstract ViewMessages Messages { get; }

        [HttpGet]
        [Authorize]
        public virtual ActionResult ConfirmationRequired(string accountName = null)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            var model = new ConfirmationViewModel(account);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ActionName("ConfirmationRequired")]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ConfirmationRequiredPost(string accountName = null)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            var confirmationUrl = Url.ConfirmEmail(account.Username, account.EmailConfirmationToken, relativeUrl: false);

            var alreadyConfirmed = account.UnconfirmedEmailAddress == null;

            ConfirmationViewModel model;
            if (!alreadyConfirmed)
            {
                MessageService.SendNewAccountEmail(new MailAddress(account.UnconfirmedEmailAddress, account.Username), confirmationUrl);

                model = new ConfirmationViewModel(account)
                {
                    SentEmail = true
                };
            }
            else
            {
                model = new ConfirmationViewModel(account);
            }
            return View(model);
        }

        [Authorize]
        public virtual async Task<ActionResult> Confirm(string username, string token)
        {
            // We don't want Login to go to this page as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            var account = GetAccount(username);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return View(new ConfirmationViewModel(account)
                {
                    WrongUsername = true,
                    SuccessfulConfirmation = false,
                });
            }

            string existingEmail = account.EmailAddress;
            var model = new ConfirmationViewModel(account);

            if (!model.AlreadyConfirmed)
            {
                try
                {
                    model.SuccessfulConfirmation = await UserService.ConfirmEmailAddress(account, token);
                }
                catch (EntityException)
                {
                    model.SuccessfulConfirmation = false;
                    model.DuplicateEmailAddress = true;
                }

                // SuccessfulConfirmation is required so that the confirm Action isn't a way to spam people.
                // Change notice not required for new accounts.
                if (model.SuccessfulConfirmation && !model.ConfirmingNewAccount)
                {
                    MessageService.SendEmailChangeNoticeToPreviousEmailAddress(account, existingEmail);

                    string returnUrl = HttpContext.GetConfirmationReturnUrl();
                    if (!String.IsNullOrEmpty(returnUrl))
                    {
                        TempData["Message"] = Messages.EmailConfirmed;
                        return SafeRedirect(returnUrl);
                    }
                }
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangeEmailSubscription(TAccountViewModel model)
        {
            var account = GetAccount(model.AccountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            await UserService.ChangeEmailSubscriptionAsync(
                account,
                model.ChangeNotifications.EmailAllowed,
                model.ChangeNotifications.NotifyPackagePushed);

            TempData["Message"] = Messages.EmailPreferencesUpdated;

            return RedirectToAction(AccountAction);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangeEmail(TAccountViewModel model)
        {
            var account = GetAccount(model.AccountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            if (!ModelState.IsValidField("ChangeEmail.NewEmail"))
            {
                return AccountView(account, model);
            }
            
            if (account.HasPassword())
            {
                if (!ModelState.IsValidField("ChangeEmail.Password"))
                {
                    return AccountView(account, model);
                }
                
                if (!AuthenticationService.ValidatePasswordCredential(account.Credentials, model.ChangeEmail.Password, out var _))
                {
                    ModelState.AddModelError("ChangeEmail.Password", Strings.CurrentPasswordIncorrect);
                    return AccountView(account, model);
                }
            }

            // No password? We can't do any additional verification...

            if (string.Equals(model.ChangeEmail.NewEmail, account.LastSavedEmailAddress, StringComparison.OrdinalIgnoreCase))
            {
                // email address unchanged - accept
                return RedirectToAction(AccountAction);
            }

            try
            {
                await UserService.ChangeEmailAddress(account, model.ChangeEmail.NewEmail);
            }
            catch (EntityException e)
            {
                ModelState.AddModelError("ChangeEmail.NewEmail", e.Message);
                return AccountView(account, model);
            }

            if (account.Confirmed)
            {
                var confirmationUrl = Url.ConfirmEmail(account.Username, account.EmailConfirmationToken, relativeUrl: false);
                MessageService.SendEmailChangeConfirmationNotice(new MailAddress(account.UnconfirmedEmailAddress, account.Username), confirmationUrl);

                TempData["Message"] = Messages.EmailUpdatedWithConfirmationRequired;
            }
            else
            {
                TempData["Message"] = Messages.EmailUpdated;
            }

            return RedirectToAction(AccountAction);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> CancelChangeEmail(TAccountViewModel model)
        {
            var account = GetAccount(model.AccountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            if (string.IsNullOrWhiteSpace(account.UnconfirmedEmailAddress))
            {
                return RedirectToAction(AccountAction);
            }

            await UserService.CancelChangeEmailAddress(account);

            TempData["Message"] = Messages.EmailUpdateCancelled;

            return RedirectToAction(AccountAction);
        }

        protected virtual TUser GetAccount(string accountName)
        {
            return UserService.FindByUsername(accountName) as TUser;
        }

        protected virtual ActionResult AccountView(TUser account, TAccountViewModel model = null)
        {
            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            model = model ?? Activator.CreateInstance<TAccountViewModel>();
            
            UpdateAccountViewModel(account, model);

            return View(AccountAction, model);
        }

        protected virtual void UpdateAccountViewModel(TUser account, TAccountViewModel model)
        {
            model.Account = account;
            model.AccountName = account.Username;

            model.CuratedFeeds = CuratedFeedService
                .GetFeedsForManager(account.Key)
                .Select(f => f.Name)
                .ToList();

            model.HasPassword = account.Credentials.Any(c => c.Type.StartsWith(CredentialTypes.Password.Prefix));
            model.CurrentEmailAddress = account.UnconfirmedEmailAddress ?? account.EmailAddress;
            model.HasConfirmedEmailAddress = !string.IsNullOrEmpty(account.EmailAddress);
            model.HasUnconfirmedEmailAddress = !string.IsNullOrEmpty(account.UnconfirmedEmailAddress);

            model.ChangeEmail = new ChangeEmailViewModel();

            model.ChangeNotifications = model.ChangeNotifications ?? new ChangeNotificationsViewModel();
            model.ChangeNotifications.EmailAllowed = account.EmailAllowed;
            model.ChangeNotifications.NotifyPackagePushed = account.NotifyPackagePushed;
        }
    }
}