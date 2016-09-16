﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery
{
    public partial class UsersController
        : AppController
    {
        private readonly ICuratedFeedService _curatedFeedService;
        private readonly IUserService _userService;
        private readonly IMessageService _messageService;
        private readonly IPackageService _packageService;
        private readonly IAppConfiguration _config;
        private readonly AuthenticationService _authService;
        private readonly ICredentialBuilder _credentialBuilder;

        public UsersController(
            ICuratedFeedService feedsQuery,
            IUserService userService,
            IPackageService packageService,
            IMessageService messageService,
            IAppConfiguration config,
            AuthenticationService authService,
            ICredentialBuilder credentialBuilder)
        {
            if (feedsQuery == null)
            {
                throw new ArgumentNullException(nameof(feedsQuery));
            }

            if (userService == null)
            {
                throw new ArgumentNullException(nameof(userService));
            }

            if (packageService == null)
            {
                throw new ArgumentNullException(nameof(packageService));
            }

            if (messageService == null)
            {
                throw new ArgumentNullException(nameof(messageService));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (authService == null)
            {
                throw new ArgumentNullException(nameof(authService));
            }

            if (credentialBuilder == null)
            {
                throw new ArgumentNullException(nameof(credentialBuilder));
            }

            _curatedFeedService = feedsQuery;
            _userService = userService;
            _packageService = packageService;
            _messageService = messageService;
            _config = config;
            _authService = authService;
            _credentialBuilder = credentialBuilder;
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult ConfirmationRequired()
        {
            User user = GetCurrentUser();
            var model = new ConfirmationViewModel
            {
                ConfirmingNewAccount = !(user.Confirmed),
                UnconfirmedEmailAddress = user.UnconfirmedEmailAddress,
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ActionName("ConfirmationRequired")]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ConfirmationRequiredPost()
        {
            User user = GetCurrentUser();
            var confirmationUrl = Url.ConfirmationUrl(
                "Confirm", "Users", user.Username, user.EmailConfirmationToken);

            var alreadyConfirmed = user.UnconfirmedEmailAddress == null;

            ConfirmationViewModel model;
            if (!alreadyConfirmed)
            {
                _messageService.SendNewAccountEmail(new MailAddress(user.UnconfirmedEmailAddress, user.Username), confirmationUrl);

                model = new ConfirmationViewModel
                {
                    ConfirmingNewAccount = !(user.Confirmed),
                    UnconfirmedEmailAddress = user.UnconfirmedEmailAddress,
                    SentEmail = true,
                };
            }
            else
            {
                model = new ConfirmationViewModel {AlreadyConfirmed = true};
            }
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult Account()
        {
            return AccountView(new AccountViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangeEmailSubscription(bool? emailAllowed, bool? notifyPackagePushed)
        {
            var user = GetCurrentUser();
            if (user == null)
            {
                return HttpNotFound();
            }
            
            await _userService.ChangeEmailSubscriptionAsync(user, 
                emailAllowed.HasValue && emailAllowed.Value, 
                notifyPackagePushed.HasValue && notifyPackagePushed.Value);

            TempData["Message"] = Strings.EmailPreferencesUpdated;
            return RedirectToAction("Account");
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult Thanks()
        {
            // No need to redirect here after someone logs in...
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            return View();
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult Packages()
        {
            var user = GetCurrentUser();
            var packages = _packageService.FindPackagesByOwner(user, includeUnlisted: true)
                .Select(p => new PackageViewModel(p)
                {
                    DownloadCount = p.PackageRegistration.DownloadCount,
                    Version = null
                }).ToList();

            var model = new ManagePackagesViewModel
            {
                Packages = packages
            };
            return View(model);
        }

        [HttpGet]
        public virtual ActionResult ForgotPassword()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            if (ModelState.IsValid)
            {
                var user = await _authService.GeneratePasswordResetToken(model.Email, Constants.PasswordResetTokenExpirationHours * 60);
                if (user != null)
                {
                    return SendPasswordResetEmail(user, forgotPassword: true);
                }

                ModelState.AddModelError("Email", Strings.CouldNotFindAnyoneWithThatEmail);
            }

            return View(model);
        }

        [HttpGet]
        public virtual ActionResult PasswordSent()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            ViewBag.Email = TempData["Email"];
            ViewBag.Expiration = Constants.PasswordResetTokenExpirationHours;
            return View();
        }

        [HttpGet]
        public virtual ActionResult ResetPassword(bool forgot)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            ViewBag.ResetTokenValid = true;
            ViewBag.ForgotPassword = forgot;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ResetPassword(string username, string token, PasswordResetViewModel model, bool forgot)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            if (!ModelState.IsValid)
            {
                return ResetPassword(forgot);
            }

            ViewBag.ForgotPassword = forgot;

            Credential credential = null;
            try
            {
                credential = await _authService.ResetPasswordWithToken(username, token, model.NewPassword);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }

            ViewBag.ResetTokenValid = credential != null;

            if (!ViewBag.ResetTokenValid)
            {
                ModelState.AddModelError("", Strings.InvalidOrExpiredPasswordResetToken);
                return View(model);
            }

            if (credential != null && !forgot)
            {
                // Setting a password, so notify the user
                _messageService.SendCredentialAddedNotice(credential.User, credential);
            }

            return RedirectToAction(
                actionName: "PasswordChanged",
                controllerName: "Users");
        }

        [Authorize]
        public virtual async Task<ActionResult> Confirm(string username, string token)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            if (!String.Equals(username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View(new ConfirmationViewModel
                    {
                        WrongUsername = true,
                        SuccessfulConfirmation = false,
                    });
            }

            var user = GetCurrentUser();

            var alreadyConfirmed = user.UnconfirmedEmailAddress == null;

            string existingEmail = user.EmailAddress;
            var model = new ConfirmationViewModel
            {
                ConfirmingNewAccount = String.IsNullOrEmpty(existingEmail),
                SuccessfulConfirmation = !alreadyConfirmed,
                AlreadyConfirmed = alreadyConfirmed
            };

            if (!alreadyConfirmed)
            {

                try
                {
                    if (!(await _userService.ConfirmEmailAddress(user, token)))
                    {
                        model.SuccessfulConfirmation = false;
                    }
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
                    _messageService.SendEmailChangeNoticeToPreviousEmailAddress(user, existingEmail);

                    string returnUrl = HttpContext.GetConfirmationReturnUrl();
                    if (!String.IsNullOrEmpty(returnUrl))
                    {
                        TempData["Message"] = "You have successfully confirmed your email address!";
                        return SafeRedirect(returnUrl);
                    }
                }
            }

            return View(model);
        }

        [HttpGet]
        public virtual ActionResult Profiles(string username, int page = 1, bool showAllPackages = false)
        {
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return HttpNotFound();
            }

            var packages = _packageService.FindPackagesByOwner(user, includeUnlisted: false)
                .OrderByDescending(p => p.PackageRegistration.DownloadCount)
                .Select(p => new PackageViewModel(p)
                {
                    DownloadCount = p.PackageRegistration.DownloadCount
                }).ToList();

            var model = new UserProfileModel(user, packages, page - 1, Constants.DefaultPackageListPageSize, Url);
            model.ShowAllPackages = showAllPackages;

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public virtual async Task<ActionResult> ChangeEmail(AccountViewModel model)
        {
            if (!ModelState.IsValidField("ChangeEmail.NewEmail"))
            {
                return AccountView(model);
            }

            var user = GetCurrentUser();
            if (user.HasPassword())
            {
                if (!ModelState.IsValidField("ChangeEmail.Password"))
                {
                    return AccountView(model);
                }

                var authUser = await _authService.Authenticate(User.Identity.Name, model.ChangeEmail.Password);
                if (authUser == null)
                {
                    ModelState.AddModelError("ChangeEmail.Password", Strings.CurrentPasswordIncorrect);
                    return AccountView(model);
                }
            }
            // No password? We can't do any additional verification...

            if (String.Equals(model.ChangeEmail.NewEmail, user.LastSavedEmailAddress, StringComparison.OrdinalIgnoreCase))
            {
                // email address unchanged - accept
                return RedirectToAction(actionName: "Account", controllerName: "Users");
            }

            try
            {
                await _userService.ChangeEmailAddress(user, model.ChangeEmail.NewEmail);
            }
            catch (EntityException e)
            {
                ModelState.AddModelError("ChangeEmail.NewEmail", e.Message);
                return AccountView(model);
            }

            if (user.Confirmed)
            {
                var confirmationUrl = Url.ConfirmationUrl(
                    "Confirm", "Users", user.Username, user.EmailConfirmationToken);
                _messageService.SendEmailChangeConfirmationNotice(new MailAddress(user.UnconfirmedEmailAddress, user.Username), confirmationUrl);

                TempData["Message"] = Strings.EmailUpdated_ConfirmationRequired;
            }
            else
            {
                TempData["Message"] = Strings.EmailUpdated;
            }

            return RedirectToAction(actionName: "Account", controllerName: "Users");
        }

        [HttpPost]
        [Authorize]
        public virtual async Task<ActionResult> CancelChangeEmail(AccountViewModel model)
        {
            var user = GetCurrentUser();

            if(string.IsNullOrWhiteSpace(user.UnconfirmedEmailAddress))
            {
                return RedirectToAction(actionName: "Account", controllerName: "Users");
            }

            await _userService.CancelChangeEmailAddress(user);

            TempData["Message"] = Strings.CancelEmailAddress;

            return RedirectToAction(actionName: "Account", controllerName: "Users");
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangePassword(AccountViewModel model)
        {
            var user = GetCurrentUser();

            var oldPassword = user.Credentials.FirstOrDefault(
                c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));

            if (oldPassword == null)
            {
                // User is requesting a password set email
                await _authService.GeneratePasswordResetToken(user, Constants.PasswordResetTokenExpirationHours * 60);

                return SendPasswordResetEmail(user, forgotPassword: false);
            }
            else
            {
                if (!ModelState.IsValidField("ChangePassword"))
                {
                    return AccountView(model);
                }

                if (!(await _authService.ChangePassword(user, model.ChangePassword.OldPassword, model.ChangePassword.NewPassword, model.ChangePassword.ResetApiKey)))
                {
                    ModelState.AddModelError("ChangePassword.OldPassword", Strings.CurrentPasswordIncorrect);
                    return AccountView(model);
                }
                
                if (model.ChangePassword.ResetApiKey)
                {
                    TempData["Message"] = Strings.PasswordChanged + " " + Strings.ApiKeyAlsoUpdated;
                }
                else
                {
                    TempData["Message"] = Strings.PasswordChanged;
                }

                return RedirectToAction("Account");
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual Task<ActionResult> RemovePassword()
        {
            var user = GetCurrentUser();
            var passwordCred = user.Credentials.SingleOrDefault(
                c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));

            return RemoveCredential(user, passwordCred, Strings.PasswordRemoved);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual Task<ActionResult> RemoveCredential(string credentialType)
        {
            var user = GetCurrentUser();
            var cred = user.Credentials.SingleOrDefault(
                c => String.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase));

            return RemoveCredential(user, cred, Strings.CredentialRemoved);
        }

        [HttpGet]
        public virtual ActionResult PasswordChanged()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> GenerateApiKey(int? expirationInDays)
        {
            // Get the user
            var user = GetCurrentUser();

            // Set expiration
            var expiration = TimeSpan.Zero;
            if (_config.ExpirationInDaysForApiKeyV1 > 0)
            {
                expiration = TimeSpan.FromDays(_config.ExpirationInDaysForApiKeyV1);

                if (expirationInDays.HasValue && expirationInDays.Value > 0)
                {
                    expiration = TimeSpan.FromDays(Math.Min(expirationInDays.Value, _config.ExpirationInDaysForApiKeyV1));
                }
            }

            // Add/Replace the API Key credential, and save to the database
            TempData["Message"] = Strings.ApiKeyReset;
            await _authService.ReplaceCredential(user, _credentialBuilder.CreateApiKey(expiration));

            return RedirectToAction("Account");
        }

        private async Task<ActionResult> RemoveCredential(User user, Credential cred, string message)
        {
            if (cred == null)
            {
                TempData["Message"] = Strings.NoCredentialToRemove;

                return RedirectToAction("Account");
            }

            // Count credentials and make sure the user can always login
            if (!String.Equals(cred.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase)
                && CountLoginCredentials(user) <= 1)
            {
                TempData["Message"] = Strings.CannotRemoveOnlyLoginCredential;
            }
            else
            {
                await _authService.RemoveCredential(user, cred);

                // Notify the user of the change
                _messageService.SendCredentialRemovedNotice(user, cred);

                TempData["Message"] = message;
            }

            return RedirectToAction("Account");
        }

        private ActionResult AccountView(AccountViewModel model)
        {
            // Load Credential info
            var user = GetCurrentUser();
            var curatedFeeds = _curatedFeedService.GetFeedsForManager(user.Key);
            var creds = user.Credentials.Select(c => _authService.DescribeCredential(c)).ToList();

            model.Credentials = creds;
            model.CuratedFeeds = curatedFeeds.Select(f => f.Name);

            model.ExpirationInDaysForApiKeyV1 = _config.ExpirationInDaysForApiKeyV1;

            return View("Account", model);
        }

        private static int CountLoginCredentials(User user)
        {
            return user.Credentials.Count(c =>
                c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase) ||
                c.Type.StartsWith(CredentialTypes.ExternalPrefix, StringComparison.OrdinalIgnoreCase));
        }

        private ActionResult SendPasswordResetEmail(User user, bool forgotPassword)
        {
            var resetPasswordUrl = Url.ConfirmationUrl(
                "ResetPassword",
                "Users",
                user.Username,
                user.PasswordResetToken,
                new { forgot = forgotPassword });
            _messageService.SendPasswordResetInstructions(user, resetPasswordUrl, forgotPassword);

            return RedirectToAction(actionName: "PasswordSent", controllerName: "Users");
        }
    }
}
