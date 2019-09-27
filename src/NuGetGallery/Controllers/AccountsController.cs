// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public abstract class AccountsController<TUser, TAccountViewModel> : AppController
        where TUser : User
        where TAccountViewModel : AccountViewModel<TUser>
    {
        public class ViewMessages
        {
            public string EmailConfirmed { get; set; }

            public string EmailPreferencesUpdated { get; set; }

            public string EmailUpdateCancelled { get; set; }
        }

        protected AuthenticationService AuthenticationService { get; }

        protected IPackageService PackageService { get; }

        protected IMessageService MessageService { get; }

        protected IUserService UserService { get; }

        protected ITelemetryService TelemetryService { get; }

        protected ISecurityPolicyService SecurityPolicyService { get; }

        protected ICertificateService CertificateService { get; }

        protected IContentObjectService ContentObjectService { get; }

        protected IMessageServiceConfiguration MessageServiceConfiguration { get; }

        protected IDeleteAccountService DeleteAccountService { get; }

        protected IIconUrlProvider IconUrlProvider { get; }

        protected IGravatarProxyService GravatarProxy { get; }

        private readonly DeleteAccountListPackageItemViewModelFactory _deleteAccountListPackageItemViewModelFactory;

        public AccountsController(
            AuthenticationService authenticationService,
            IPackageService packageService,
            IMessageService messageService,
            IUserService userService,
            ITelemetryService telemetryService,
            ISecurityPolicyService securityPolicyService,
            ICertificateService certificateService,
            IContentObjectService contentObjectService,
            IMessageServiceConfiguration messageServiceConfiguration,
            IDeleteAccountService deleteAccountService,
            IIconUrlProvider iconUrlProvider,
            IGravatarProxyService gravatarProxy)
        {
            AuthenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            PackageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            MessageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            UserService = userService ?? throw new ArgumentNullException(nameof(userService));
            TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            SecurityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
            CertificateService = certificateService ?? throw new ArgumentNullException(nameof(certificateService));
            ContentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            MessageServiceConfiguration = messageServiceConfiguration ?? throw new ArgumentNullException(nameof(messageServiceConfiguration));
            DeleteAccountService = deleteAccountService ?? throw new ArgumentNullException(nameof(deleteAccountService));
            IconUrlProvider = iconUrlProvider ?? throw new ArgumentNullException(nameof(iconUrlProvider));
            GravatarProxy = gravatarProxy ?? throw new ArgumentNullException(nameof(gravatarProxy));

            _deleteAccountListPackageItemViewModelFactory = new DeleteAccountListPackageItemViewModelFactory(PackageService, IconUrlProvider);
        }

        public abstract string AccountAction { get; }

        protected internal abstract ViewMessages Messages { get; }

        [HttpGet]
        [UIAuthorize(allowDiscontinuedLogins: true)]
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

        [UIAuthorize(allowDiscontinuedLogins: true)]
        [HttpPost]
        [ActionName("ConfirmationRequired")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ConfirmationRequiredPost(string accountName = null)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            var hasUnconfirmedEmailAddress = account.UnconfirmedEmailAddress != null;

            ConfirmationViewModel model;
            if (hasUnconfirmedEmailAddress)
            {
                if (account.EmailAddress == null)
                {
                    await SendNewAccountEmailAsync(account);
                }
                else
                {
                    await SendEmailChangedConfirmationNoticeAsync(account);
                }

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

        protected abstract Task SendNewAccountEmailAsync(User account);

        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> Confirm(string accountName, string token)
        {
            // We don't want Login to go to this page as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;

            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return View(new ConfirmationViewModel(accountName)
                {
                    WrongUsername = true,
                    SuccessfulConfirmation = false
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
                    var message = new EmailChangeNoticeToPreviousEmailAddressMessage(MessageServiceConfiguration, account, existingEmail);
                    await MessageService.SendMessageAsync(message);

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

        [UIAuthorize]
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
        [UIAuthorize]
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

            if (account.HasPasswordCredential())
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

            if (account.Confirmed && !string.IsNullOrEmpty(account.UnconfirmedEmailAddress))
            {
                await SendEmailChangedConfirmationNoticeAsync(account);
            }

            return RedirectToAction(AccountAction);
        }

        protected abstract Task SendEmailChangedConfirmationNoticeAsync(User account);

        [HttpPost]
        [UIAuthorize]
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

            if (!string.IsNullOrWhiteSpace(account.UnconfirmedEmailAddress))
            {
                await UserService.CancelChangeEmailAddress(account);

                TempData["Message"] = Messages.EmailUpdateCancelled;
            }

            return RedirectToAction(AccountAction);
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult DeleteRequest(string accountName = null)
        {
            var accountToDelete = GetAccount(accountName);

            if (accountToDelete == null || accountToDelete.IsDeleted)
            {
                return HttpNotFound();
            }

            if (ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), accountToDelete)
                    != PermissionsCheckResult.Allowed)
            {
                return HttpNotFound();
            }

            return View("DeleteAccount", GetDeleteAccountViewModel(accountToDelete));
        }

        [HttpGet]
        [UIAuthorize(Roles = "Admins")]
        public virtual ActionResult Delete(string accountName)
        {
            var accountToDelete = UserService.FindByUsername(accountName) as TUser;
            if (accountToDelete == null || accountToDelete.IsDeleted)
            {
                return HttpNotFound();
            }

            return View(GetDeleteAccountViewName(), GetDeleteAccountViewModel(accountToDelete));
        }

        [HttpDelete]
        [UIAuthorize(Roles = "Admins")]
        [RequiresAccountConfirmation("Delete account")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Delete(DeleteAccountAsAdminViewModel model)
        {
            var accountToDelete = UserService.FindByUsername(model.AccountName) as TUser;
            if (accountToDelete == null || accountToDelete.IsDeleted)
            {
                return View("DeleteAccountStatus", new DeleteAccountStatus()
                {
                    AccountName = model.AccountName,
                    Description = $"Account {model.AccountName} not found.",
                    Success = false
                });
            }
            else
            {
                var admin = GetCurrentUser();
                var status = await DeleteAccountService.DeleteAccountAsync(
                    userToBeDeleted: accountToDelete,
                    userToExecuteTheDelete: admin,
                    orphanPackagePolicy: model.ShouldUnlist ? AccountDeletionOrphanPackagePolicy.UnlistOrphans : AccountDeletionOrphanPackagePolicy.KeepOrphans);
                return View("DeleteAccountStatus", status);
            }
        }

        protected abstract string GetDeleteAccountViewName();

        protected abstract DeleteAccountViewModel GetDeleteAccountViewModel(TUser account);

        public abstract Task<ActionResult> RequestAccountDeletion(string accountName = null);

        protected List<DeleteAccountListPackageItemViewModel> GetOwnedPackagesViewModels(User account)
        {
            return PackageService
                 .FindPackagesByAnyMatchingOwner(account, includeUnlisted: true)
                 .Select(p => CreateDeleteAccountListPackageItemViewModel(p, account, GetCurrentUser()))
                 .ToList();
        }

        private DeleteAccountListPackageItemViewModel CreateDeleteAccountListPackageItemViewModel(
            Package package,
            User userToDelete,
            User currentUser)
        {
            return _deleteAccountListPackageItemViewModelFactory.Create(package, userToDelete, currentUser);
        }


        protected virtual TUser GetAccount(string accountName)
        {
            return UserService.FindByUsername(accountName) as TUser;
        }

        protected virtual ActionResult AccountView(TUser account, TAccountViewModel model = null)
        {
            if (account == null
                || ActionsRequiringPermissions.ViewAccount.CheckPermissions(GetCurrentUser(), account)
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

            var currentUser = GetCurrentUser();

            model.CanManage = ActionsRequiringPermissions.ManageAccount.CheckPermissions(
                currentUser, account) == PermissionsCheckResult.Allowed;

            model.IsCertificatesUIEnabled = ContentObjectService.CertificatesConfiguration?.IsUIEnabledForUser(currentUser) ?? false;
            model.WasMultiFactorAuthenticated = User.WasMultiFactorAuthenticated();

            model.HasPassword = account.Credentials.Any(c => c.IsPassword());
            model.CurrentEmailAddress = account.UnconfirmedEmailAddress ?? account.EmailAddress;
            model.HasConfirmedEmailAddress = !string.IsNullOrEmpty(account.EmailAddress);
            model.HasUnconfirmedEmailAddress = !string.IsNullOrEmpty(account.UnconfirmedEmailAddress);

            model.ChangeEmail = new ChangeEmailViewModel();

            model.ChangeNotifications = model.ChangeNotifications ?? new ChangeNotificationsViewModel();
            model.ChangeNotifications.EmailAllowed = account.EmailAllowed;
            model.ChangeNotifications.NotifyPackagePushed = account.NotifyPackagePushed;
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("add a certificate")]
        public virtual async Task<JsonResult> AddCertificate(string accountName, HttpPostedFileBase uploadFile)
        {
            if (uploadFile == null)
            {
                return Json(HttpStatusCode.BadRequest, new[] { Strings.CertificateFileIsRequired });
            }

            var currentUser = GetCurrentUser();
            var account = GetAccount(accountName);

            if (currentUser == null)
            {
                return Json(HttpStatusCode.Unauthorized);
            }

            if (account == null)
            {
                return Json(HttpStatusCode.NotFound);
            }

            if (!CanManageCertificates(currentUser, account))
            {
                return Json(HttpStatusCode.Forbidden, new { Strings.Unauthorized });
            }

            Certificate certificate;

            try
            {
                using (var uploadStream = uploadFile.InputStream)
                {
                    certificate = await CertificateService.AddCertificateAsync(uploadFile);
                }

                await CertificateService.ActivateCertificateAsync(certificate.Thumbprint, account);
            }
            catch (UserSafeException ex)
            {
                ex.Log();

                return Json(HttpStatusCode.BadRequest, new[] { ex.Message });
            }

            var activeCertificateCount = CertificateService.GetCertificates(account).Count();

            if (activeCertificateCount == 1 &&
                SecurityPolicyService.IsSubscribed(account, AutomaticallyOverwriteRequiredSignerPolicy.PolicyName))
            {
                await PackageService.SetRequiredSignerAsync(account);
            }

            return Json(HttpStatusCode.Created, new { certificate.Thumbprint });
        }

        [HttpDelete]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("delete a certificate")]
        public virtual async Task<JsonResult> DeleteCertificate(string accountName, string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                return Json(HttpStatusCode.BadRequest);
            }

            var currentUser = GetCurrentUser();
            var account = GetAccount(accountName);

            if (currentUser == null)
            {
                return Json(HttpStatusCode.Unauthorized);
            }

            if (account == null)
            {
                return Json(HttpStatusCode.NotFound);
            }

            if (!CanManageCertificates(currentUser, account))
            {
                return Json(HttpStatusCode.Forbidden, new { Strings.Unauthorized });
            }

            await CertificateService.DeactivateCertificateAsync(thumbprint, account);

            return Json(HttpStatusCode.OK);
        }

        [HttpGet]
        [UIAuthorize]
        public virtual JsonResult GetCertificates(string accountName)
        {
            var currentUser = GetCurrentUser();
            var account = GetAccount(accountName);

            if (currentUser == null)
            {
                return Json(HttpStatusCode.Unauthorized);
            }

            if (account == null)
            {
                return Json(HttpStatusCode.NotFound);
            }

            if (ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, account)
                != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden);
            }

            var canManageCertificates = CanManageCertificates(currentUser, account);
            var template = GetDeleteCertificateForAccountTemplate(accountName);

            var certificates = CertificateService.GetCertificates(account)
                .Select(certificate =>
                {
                    string deactivateUrl = null;

                    if (canManageCertificates)
                    {
                        deactivateUrl = template.Resolve(certificate.Thumbprint);
                    }

                    return new ListCertificateItemViewModel(certificate, deactivateUrl);
                });

            return Json(HttpStatusCode.OK, certificates, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [UIAuthorize]
        public virtual JsonResult GetCertificate(string accountName, string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                return Json(HttpStatusCode.BadRequest);
            }

            var currentUser = GetCurrentUser();
            var account = GetAccount(accountName);

            if (currentUser == null)
            {
                return Json(HttpStatusCode.Unauthorized);
            }

            if (account == null)
            {
                return Json(HttpStatusCode.NotFound);
            }

            if (ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, account)
                != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden);
            }

            var canManageCertificates = CanManageCertificates(currentUser, account);
            var template = GetDeleteCertificateForAccountTemplate(accountName);

            var certificates = CertificateService.GetCertificates(account)
                .Where(certificate => certificate.Thumbprint == thumbprint)
                .Select(certificate =>
                {
                    string deactivateUrl = null;

                    if (canManageCertificates)
                    {
                        deactivateUrl = template.Resolve(certificate.Thumbprint);
                    }

                    return new ListCertificateItemViewModel(certificate, deactivateUrl);
                });

            return Json(HttpStatusCode.OK, certificates, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [OutputCache(
            Duration = GalleryConstants.GravatarCacheDurationSeconds,
            Location = OutputCacheLocation.Downstream,
            VaryByParam = "imageSize")]
        public async Task<ActionResult> GetAvatar(
            string accountName,
            int? imageSize = GalleryConstants.GravatarImageSize)
        {
            var result = await GravatarProxy.GetAvatarOrNullAsync(accountName, imageSize ?? GalleryConstants.GravatarImageSize);
            if (result == null)
            {
                return HttpNotFound();
            }

            return File(result.AvatarStream, result.ContentType);
        }

        private bool CanManageCertificates(User currentUser, User account)
        {
            var wasAADLoginOrMultiFactorAuthenticated = User.WasMultiFactorAuthenticated() || User.WasAzureActiveDirectoryAccountUsedForSignin();
            return wasAADLoginOrMultiFactorAuthenticated
                && ActionsRequiringPermissions.ManageAccount.CheckPermissions(currentUser, account) == PermissionsCheckResult.Allowed;
        }

        protected abstract RouteUrlTemplate<string> GetDeleteCertificateForAccountTemplate(string accountName);
    }
}