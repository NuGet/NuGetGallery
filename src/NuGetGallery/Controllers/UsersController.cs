// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Frameworks;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Security;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery
{
    public partial class UsersController
        : AccountsController<User, UserAccountViewModel>
    {
        private readonly IPackageOwnerRequestService _packageOwnerRequestService;
        private readonly IAppConfiguration _config;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly ListPackageItemRequiredSignerViewModelFactory _listPackageItemRequiredSignerViewModelFactory;
        private readonly ListPackageItemViewModelFactory _listPackageItemViewModelFactory;
        private readonly ISupportRequestService _supportRequestService;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPackageVulnerabilitiesService _packageVulnerabilitiesService;
        private readonly IFederatedCredentialService _federatedCredentialService;

        public UsersController(
            IUserService userService,
            IPackageService packageService,
            IPackageOwnerRequestService packageOwnerRequestService,
            IMessageService messageService,
            IAppConfiguration config,
            AuthenticationService authService,
            ICredentialBuilder credentialBuilder,
            IDeleteAccountService deleteAccountService,
            ISupportRequestService supportRequestService,
            ITelemetryService telemetryService,
            ISecurityPolicyService securityPolicyService,
            ICertificateService certificateService,
            IContentObjectService contentObjectService,
            IFeatureFlagService featureFlagService,
            IPackageVulnerabilitiesService packageVulnerabilitiesService,
            IMessageServiceConfiguration messageServiceConfiguration,
            IIconUrlProvider iconUrlProvider,
            IGravatarProxyService gravatarProxy,
            IPackageFrameworkCompatibilityFactory frameworkCompatibilityFactory,
            IFederatedCredentialService federatedCredentialService,
            IFederatedCredentialRepository federatedCredentialRepository)
            : base(
                  authService,
                  packageService,
                  messageService,
                  userService,
                  telemetryService,
                  securityPolicyService,
                  certificateService,
                  contentObjectService,
                  messageServiceConfiguration,
                  deleteAccountService,
                  iconUrlProvider,
                  gravatarProxy,
                  featureFlagService,
                  frameworkCompatibilityFactory)
        {
            _packageOwnerRequestService = packageOwnerRequestService ?? throw new ArgumentNullException(nameof(packageOwnerRequestService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _supportRequestService = supportRequestService ?? throw new ArgumentNullException(nameof(supportRequestService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _packageVulnerabilitiesService = packageVulnerabilitiesService ?? throw new ArgumentNullException(nameof(packageVulnerabilitiesService));
            _federatedCredentialService = federatedCredentialService ?? throw new ArgumentNullException(nameof(federatedCredentialService));

            _listPackageItemRequiredSignerViewModelFactory = new ListPackageItemRequiredSignerViewModelFactory(
                securityPolicyService, iconUrlProvider, packageVulnerabilitiesService, frameworkCompatibilityFactory, featureFlagService);
            _listPackageItemViewModelFactory = new ListPackageItemViewModelFactory(iconUrlProvider, frameworkCompatibilityFactory, _featureFlagService);
        }

        public override string AccountAction => nameof(Account);

        protected internal override ViewMessages Messages => new ViewMessages
        {
            EmailConfirmed = Strings.UserEmailConfirmed,
            EmailPreferencesUpdated = Strings.UserEmailPreferencesUpdated,
            EmailUpdateCancelled = Strings.UserEmailUpdateCancelled
        };

        protected override Task SendNewAccountEmailAsync(User account)
        {
            var message = new NewAccountMessage(
                MessageServiceConfiguration,
                account,
                Url.ConfirmEmail(account.Username, account.EmailConfirmationToken, relativeUrl: false));

            return MessageService.SendMessageAsync(message);
        }

        protected override Task SendEmailChangedConfirmationNoticeAsync(User account)
        {
            var message = new EmailChangeConfirmationMessage(
                MessageServiceConfiguration,
                account,
                Url.ConfirmEmail(account.Username, account.EmailConfirmationToken, relativeUrl: false));

            return MessageService.SendMessageAsync(message);
        }

        protected override User GetAccount(string accountName)
        {
            var currentUser = GetCurrentUser();
            if (string.IsNullOrEmpty(accountName) ||
                currentUser.Username.Equals(accountName, StringComparison.InvariantCultureIgnoreCase))
            {
                return currentUser;
            }
            return null;
        }

        protected override string GetDeleteAccountViewName() => "DeleteUserAccount";

        protected override DeleteAccountViewModel GetDeleteAccountViewModel(User account)
        {
            return new DeleteUserViewModel(account, PackageService, GetOwnedPackagesViewModels(account), _supportRequestService);
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult Account()
        {
            return AccountView(GetCurrentUser());
        }

        [HttpGet]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual ActionResult TransformToOrganization()
        {
            var accountToTransform = GetCurrentUser();

            string errorReason;
            if (!UserService.CanTransformUserToOrganization(accountToTransform, out errorReason))
            {
                return TransformToOrganizationFailed(errorReason);
            }

            var transformRequest = accountToTransform.OrganizationMigrationRequest;
            if (transformRequest != null)
            {
                TempData["Message"] = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_RequestExists, transformRequest.AdminUser.Username);
            }

            return View(new TransformAccountViewModel());
        }

        [HttpPost]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> TransformToOrganization(TransformAccountViewModel transformViewModel)
        {
            var accountToTransform = GetCurrentUser();

            var adminUsername = transformViewModel.AdminUsername;
            if (Regex.IsMatch(adminUsername, GalleryConstants.EmailValidationRegex, RegexOptions.None, GalleryConstants.EmailValidationRegexTimeout))
            {
                ModelState.AddModelError(string.Empty, Strings.TransformAccount_AdminNameIsEmail);
                return View(transformViewModel);
            }

            var adminUser = UserService.FindByUsername(adminUsername);
            if (adminUser == null)
            {
                ModelState.AddModelError(string.Empty, string.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountDoesNotExist, adminUsername));
                return View(transformViewModel);
            }

            if (!UserService.CanTransformUserToOrganization(accountToTransform, adminUser, out var errorReason))
            {
                ModelState.AddModelError(string.Empty, errorReason);
                return View(transformViewModel);
            }

            // Get the user from the previous organization migration request (if there was one) so we can notify them that their request has been cancelled.
            var existingTransformRequestUser = accountToTransform.OrganizationMigrationRequest?.AdminUser;

            await UserService.RequestTransformToOrganizationAccount(accountToTransform, adminUser);

            if (existingTransformRequestUser != null)
            {
                var emailMessage = new OrganizationTransformRejectedMessage(_config, accountToTransform, existingTransformRequestUser, isCanceledByAdmin: false);
                await MessageService.SendMessageAsync(emailMessage);
            }

            var organizationTransformRequestMessage = new OrganizationTransformRequestMessage(
                _config,
                accountToTransform,
                adminUser,
                profileUrl: Url.User(accountToTransform, relativeUrl: false, supportEmail: true),
                confirmationUrl: Url.ConfirmTransformAccount(accountToTransform, relativeUrl: false, supportEmail: true),
                rejectionUrl: Url.RejectTransformAccount(accountToTransform, relativeUrl: false, supportEmail: true));
            await MessageService.SendMessageAsync(organizationTransformRequestMessage);

            var organizationTransformInitiatedMessage = new OrganizationTransformInitiatedMessage(
                _config,
                accountToTransform,
                adminUser,
                cancellationUrl: Url.CancelTransformAccount(accountToTransform, relativeUrl: false));
            await MessageService.SendMessageAsync(organizationTransformInitiatedMessage);

            TelemetryService.TrackOrganizationTransformInitiated(accountToTransform);

            // sign out pending organization and prompt for admin sign in
            OwinContext.Authentication.SignOut();

            TempData[GalleryConstants.ReturnUrlMessageViewDataKey] = String.Format(CultureInfo.CurrentCulture,
                Strings.TransformAccount_SignInToConfirm, adminUser.Username, accountToTransform.Username);

            var returnUrl = Url.ConfirmTransformAccount(accountToTransform);

            return Redirect(Url.LogOn(returnUrl));
        }

        [HttpGet]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> ConfirmTransformToOrganizationRedirect(string accountNameToTransform, string token)
        {
            return await ConfirmTransformToOrganizationAsync(accountNameToTransform, token, redirect: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> ConfirmTransformToOrganization(string accountNameToTransform, string token)
        {
            return await ConfirmTransformToOrganizationAsync(accountNameToTransform, token, redirect: false);
        }

        private async Task<ActionResult> ConfirmTransformToOrganizationAsync(string accountNameToTransform, string token, bool redirect)
        {
            var adminUser = GetCurrentUser();

            string errorReason;
            var accountToTransform = UserService.FindByUsername(accountNameToTransform);
            if (accountToTransform == null)
            {
                errorReason = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_OrganizationAccountDoesNotExist, accountNameToTransform);
                return TransformToOrganizationFailed(errorReason);
            }

            if (!UserService.CanTransformUserToOrganization(accountToTransform, out errorReason))
            {
                return TransformToOrganizationFailed(errorReason);
            }

            if (accountToTransform.OrganizationMigrationRequest != null
                && accountToTransform.OrganizationMigrationRequest.ConfirmationToken == token
                && !accountToTransform.OrganizationMigrationRequest.AdminUser.MatchesUser(adminUser))
            {
                return TransformToOrganizationFailed(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.TransformAccount_SignInToConfirm,
                    accountToTransform.OrganizationMigrationRequest.AdminUser.Username,
                    accountToTransform.Username));
            }

            if (redirect)
            {
                return Redirect(Url.ManageMyOrganizations());
            }

            if (!await UserService.TransformUserToOrganization(accountToTransform, adminUser, token))
            {
                errorReason = Strings.TransformAccount_Failed;
                return TransformToOrganizationFailed(errorReason);
            }

            var emailMessage = new OrganizationTransformAcceptedMessage(_config, accountToTransform, adminUser);
            await MessageService.SendMessageAsync(emailMessage);

            TelemetryService.TrackOrganizationTransformCompleted(accountToTransform);

            TempData["Message"] = string.Format(CultureInfo.CurrentCulture,
                Strings.TransformAccount_Success, accountNameToTransform);

            return Redirect(Url.ManageMyOrganization(accountNameToTransform));
        }

        [HttpGet]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> RejectTransformToOrganizationRedirect(string accountNameToTransform, string token)
        {
            return await RejectTransformToOrganizationAsync(accountNameToTransform, token, redirect: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> RejectTransformToOrganization(string accountNameToTransform, string token)
        {
            return await RejectTransformToOrganizationAsync(accountNameToTransform, token, redirect: false);
        }

        private async Task<ActionResult> RejectTransformToOrganizationAsync(string accountNameToTransform, string token, bool redirect)
        {
            var adminUser = GetCurrentUser();

            string message;
            var accountToTransform = UserService.FindByUsername(accountNameToTransform);
            if (accountToTransform == null)
            {
                message = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_OrganizationAccountDoesNotExist, accountNameToTransform);
            }
            else
            {
                if (redirect)
                {
                    return Redirect(Url.ManageMyOrganizations());
                }

                if (await UserService.RejectTransformUserToOrganizationRequest(accountToTransform, adminUser, token))
                {
                    var emailMessage = new OrganizationTransformRejectedMessage(_config, accountToTransform, adminUser, isCanceledByAdmin: true);
                    await MessageService.SendMessageAsync(emailMessage);

                    TelemetryService.TrackOrganizationTransformDeclined(accountToTransform);

                    message = string.Format(CultureInfo.CurrentCulture,
                        Strings.TransformAccount_Rejected, accountNameToTransform);
                }
                else
                {
                    message = Strings.TransformAccount_FailedMissingRequestToCancel;
                }
            }

            TempData["Message"] = message;

            return RedirectToAction(actionName: "Home", controllerName: "Pages");
        }

        [HttpGet]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> CancelTransformToOrganizationRedirect(string token)
        {
            return await CancelTransformToOrganizationAsync(token, redirect: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual async Task<ActionResult> CancelTransformToOrganization(string token)
        {
            return await CancelTransformToOrganizationAsync(token, redirect: false);
        }

        private async Task<ActionResult> CancelTransformToOrganizationAsync(string token, bool redirect)
        {
            var accountToTransform = GetCurrentUser();
            var adminUser = accountToTransform.OrganizationMigrationRequest?.AdminUser;

            if (redirect)
            {
                return Redirect(Url.ManageMyOrganizations());
            }

            if (await UserService.CancelTransformUserToOrganizationRequest(accountToTransform, token))
            {
                var emailMessage = new OrganizationTransformRejectedMessage(_config, accountToTransform, adminUser, isCanceledByAdmin: false);
                await MessageService.SendMessageAsync(emailMessage);

                TelemetryService.TrackOrganizationTransformCancelled(accountToTransform);

                TempData["Message"] = String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_Cancelled);
            }
            else
            {
                TempData["ErrorMessage"] = Strings.TransformAccount_FailedMissingRequestToCancel;
            }

            return RedirectToAction(actionName: "Home", controllerName: "Pages");
        }

        private ActionResult TransformToOrganizationFailed(string errorMessage)
        {
            return View("TransformToOrganizationFailed", new TransformAccountFailedViewModel(errorMessage));
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public override async Task<ActionResult> RequestAccountDeletion(string accountName = null)
        {
            var user = GetAccount(accountName);

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }
            TelemetryService.TrackRequestForAccountDeletion(user);

            if (_config.SelfServiceAccountDeleteEnabled && _featureFlagService.IsSelfServiceAccountDeleteEnabled())
            {
                return await DeleteAndCheckSuccess(user);
            }
            else
            {
                if (!user.Confirmed)
                {
                    // Unconfirmed users can be deleted immediately without creating a support request.
                    return await DeleteAndCheckSuccess(user);
                }

                var isSupportRequestCreated = await _supportRequestService.TryAddDeleteSupportRequestAsync(user);
                if (isSupportRequestCreated)
                {
                    var emailMessage = new AccountDeleteNoticeMessage(MessageServiceConfiguration, user);
                    await MessageService.SendMessageAsync(emailMessage);

                    return RedirectToAction(nameof(DeleteRequest));
                }
                else
                {
                    TempData["RequestFailedMessage"] = Strings.AccountDelete_CreateSupportRequestFails;

                    return RedirectToAction(nameof(DeleteRequest));
                }
            }
        }

        private async Task<ActionResult> DeleteAndCheckSuccess(User user)
        {
            DeleteAccountStatus accountDeleteStatus = await DeleteAccountService.DeleteAccountAsync(userToBeDeleted: user,
                        userToExecuteTheDelete: user,
                        orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);
            if (!accountDeleteStatus.Success)
            {
                TempData["RequestFailedMessage"] = Strings.AccountSelfDelete_Fail;
                return RedirectToAction("DeleteRequest");
            }
            OwinContext.Authentication.SignOut();
            TempData["Message"] = Strings.AccountDelete_SelfServiceSuccess;
            return SafeRedirect(Url.Home(false));
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult ApiKeys()
        {
            var currentUser = GetCurrentUser();

            // Get API keys
            if (!GetCredentialGroups(currentUser).TryGetValue(CredentialKind.Token, out List<CredentialViewModel> credentials))
            {
                credentials = new List<CredentialViewModel>();
            }

            var apiKeys = credentials
                .Select(c => new ApiKeyViewModel(c))
                .ToList();

            // Get package owners (user's self or organizations)
            var owners = new List<ApiKeyOwnerViewModel>
            {
                CreateApiKeyOwnerViewModel(currentUser, currentUser)
            };

            owners.AddRange(currentUser.Organizations
                .Select(o => CreateApiKeyOwnerViewModel(currentUser, o.Organization)));

            var anyWithDeprecationApi =
                _featureFlagService.IsManageDeprecationApiEnabled(currentUser)
                || currentUser.Organizations.Any(m => _featureFlagService.IsManageDeprecationApiEnabled(m.Organization));

            var model = new ApiKeyListViewModel
            {
                ApiKeys = apiKeys,
                ExpirationInDaysForApiKeyV1 = _config.ExpirationInDaysForApiKeyV1,
                PackageOwners = owners.Where(o => o.CanPushNew || o.CanPushExisting || o.CanUnlist).ToList(),
                IsDeprecationApiEnabled = anyWithDeprecationApi,
            };

            return View("ApiKeys", model);
        }

        private ApiKeyOwnerViewModel CreateApiKeyOwnerViewModel(User currentUser, User account)
        {
            return new ApiKeyOwnerViewModel(
                account.Username,
                ActionsRequiringPermissions.UploadNewPackageId.IsAllowedOnBehalfOfAccount(currentUser, account),
                ActionsRequiringPermissions.UploadNewPackageVersion.IsAllowedOnBehalfOfAccount(currentUser, account),
                ActionsRequiringPermissions.UnlistOrRelistPackage.IsAllowedOnBehalfOfAccount(currentUser, account),
                packageIds: PackageService.FindPackageRegistrationsByOwner(account)
                                .Select(p => p.Id)
                                .OrderBy(i => i)
                                .ToList());
        }

        [HttpGet]
        [UIAuthorize(allowDiscontinuedLogins: true)]
        public virtual ActionResult Thanks()
        {
            // No need to redirect here after someone logs in...
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;
            return View();
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult Packages()
        {
            var currentUser = GetCurrentUser();

            var owners = new List<ListPackageOwnerViewModel> {
                new ListPackageOwnerViewModel
                {
                    Username = "All packages"
                },
                new ListPackageOwnerViewModel(currentUser)
            }.Concat(currentUser.Organizations.Select(o => new ListPackageOwnerViewModel(o.Organization)));

            var wasAADLoginOrMultiFactorAuthenticated = User.WasMultiFactorAuthenticated() || User.WasAzureActiveDirectoryAccountUsedForSignin();

            var packages = PackageService.FindPackagesByAnyMatchingOwner(currentUser, includeUnlisted: true);

            var listedPackages = GetPackages(packages, currentUser, wasAADLoginOrMultiFactorAuthenticated,
                p => p.Listed && p.PackageStatusKey == PackageStatus.Available);

            var unlistedPackages = GetPackages(packages, currentUser, wasAADLoginOrMultiFactorAuthenticated,
                p => !p.Listed || p.PackageStatusKey != PackageStatus.Available);

            // find all received ownership requests
            var userReceived = _packageOwnerRequestService.GetPackageOwnershipRequests(newOwner: currentUser);
            var orgReceived = currentUser.Organizations
                .Where(m => ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(currentUser, m.Organization) == PermissionsCheckResult.Allowed)
                .SelectMany(m => _packageOwnerRequestService.GetPackageOwnershipRequests(newOwner: m.Organization));
            var received = userReceived.Union(orgReceived);

            // find all sent ownership requests
            var userSent = _packageOwnerRequestService.GetPackageOwnershipRequests(requestingOwner: currentUser);
            var orgSent = currentUser.Organizations
                .Where(m => ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(currentUser, m.Organization) == PermissionsCheckResult.Allowed)
                .SelectMany(m => _packageOwnerRequestService.GetPackageOwnershipRequests(requestingOwner: m.Organization));
            var sent = userSent.Union(orgSent);

            var ownerRequests = CreateOwnerRequestsViewModel(received, sent, currentUser);

            var userReservedNamespaces = currentUser.ReservedNamespaces;
            var organizationsReservedNamespaces = currentUser.Organizations.SelectMany(m => m.Organization.ReservedNamespaces);

            var reservedPrefixes = new ReservedNamespaceListViewModel(userReservedNamespaces.Union(organizationsReservedNamespaces).ToArray());

            var model = new ManagePackagesViewModel
            {
                User = currentUser,
                Owners = owners,
                ListedPackages = listedPackages,
                UnlistedPackages = unlistedPackages,
                OwnerRequests = ownerRequests,
                ReservedNamespaces = reservedPrefixes,
                WasMultiFactorAuthenticated = User.WasMultiFactorAuthenticated(),
                IsCertificatesUIEnabled = ContentObjectService.CertificatesConfiguration?.IsUIEnabledForUser(currentUser) ?? false,
                IsManagePackagesVulnerabilitiesEnabled = _featureFlagService.IsManagePackagesVulnerabilitiesEnabled()
            };

            return View(model);
        }

        /// <summary>
        /// Returns all packages based on the predicate, with the VersionSortOrder populated
        /// </summary>
        /// <param name="packages"></param>
        /// <param name="currentUser"></param>
        /// <param name="wasAADLoginOrMultiFactorAuthenticated"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        private List<ListPackageItemRequiredSignerViewModel> GetPackages(
            IEnumerable<Package> packages,
            User currentUser,
            bool wasAADLoginOrMultiFactorAuthenticated,
            Func<Package, bool> predicate)
        {
            var listedPackages = packages
                .Where(p => predicate(p))
                .Select(p => _listPackageItemRequiredSignerViewModelFactory.Create(p, currentUser, wasAADLoginOrMultiFactorAuthenticated))
                .OrderBy(p => NuGetVersion.Parse(p.FullVersion))
                .ToList();

            for (int i = 0; i < listedPackages.Count; i++)
            {
                listedPackages[i].VersionSortOrder = i;
            }

            listedPackages.Sort((x, y) => string.Compare(x.Id, y.Id, StringComparison.OrdinalIgnoreCase));

            return listedPackages;
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult Organizations()
        {
            var currentUser = GetCurrentUser();

            var model = new ManageOrganizationsViewModel(currentUser, PackageService);

            return View(model);
        }

        [HttpGet]
        public virtual ActionResult ForgotPassword()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;

            var model = new ForgotPasswordViewModel();
            model.IsPasswordLoginEnabled = _featureFlagService.IsNuGetAccountPasswordLoginEnabled();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateRecaptchaResponse]
        public virtual async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if(!_featureFlagService.IsNuGetAccountPasswordLoginEnabled() && !ContentObjectService.LoginDiscontinuationConfiguration.IsEmailInExceptionsList(model.Email))
            {
                ModelState.AddModelError(string.Empty, Strings.ForgotPassword_Disabled_Error);

                return View(model);
            }

            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;

            if (ModelState.IsValid)
            {
                var result = await AuthenticationService.GeneratePasswordResetToken(model.Email, GalleryConstants.PasswordResetTokenExpirationHours * 60);
                switch (result.Type)
                {
                    case PasswordResetResultType.UserNotConfirmed:
                        ModelState.AddModelError(string.Empty, Strings.UserIsNotYetConfirmed);
                        break;
                    case PasswordResetResultType.UserNotFound:
                        ModelState.AddModelError(string.Empty, Strings.CouldNotFindAnyoneWithThatUsernameOrEmail);
                        break;
                    case PasswordResetResultType.Success:
                        return await SendPasswordResetEmailAsync(result.User, forgotPassword: true);
                    default:
                        throw new NotImplementedException($"The password reset result type '{result.Type}' is not supported.");
                }
            }

            return View(model);
        }

        [HttpGet]
        public virtual ActionResult PasswordSent()
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;

            ViewBag.Email = TempData["Email"];
            ViewBag.Expiration = GalleryConstants.PasswordResetTokenExpirationHours;
            return View();
        }

        [HttpGet]
        public virtual ActionResult ResetPassword(bool forgot)
        {
            // We don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;

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
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = null;

            if (!ModelState.IsValid)
            {
                return ResetPassword(forgot);
            }

            ViewBag.ForgotPassword = forgot;

            Credential credential = null;
            try
            {
                credential = await AuthenticationService.ResetPasswordWithToken(username, token, model.NewPassword);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }

            ViewBag.ResetTokenValid = credential != null;

            if (!ViewBag.ResetTokenValid)
            {
                ModelState.AddModelError(string.Empty, Strings.InvalidOrExpiredPasswordResetToken);
                return View(model);
            }

            if (credential != null && !forgot)
            {
                // Setting a password, so notify the user
                var emailMessage = new CredentialAddedMessage(
                    _config,
                    credential.User,
                    AuthenticationService.DescribeCredential(credential).GetCredentialTypeInfo());
                await MessageService.SendMessageAsync(emailMessage);
            }

            return RedirectToAction("PasswordChanged");
        }

        [HttpGet]
        public virtual ActionResult Profiles(string username, int page = 1)
        {
            var currentUser = GetCurrentUser();
            var user = UserService.FindByUsername(username);
            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }

            var usesDatabasePaging = _featureFlagService.IsProfileLoadOptimizationEnabled();

            var packages = new List<ListPackageItemViewModel>();

            var totalDownloadCount = 0L;
            var packageCount = 0;

            if (!usesDatabasePaging)
            {
                packages = PackageService.FindPackagesByOwner(user, includeUnlisted: false)
                    .Where(p => p.PackageStatusKey == PackageStatus.Available)
                    .OrderByDescending(p => p.PackageRegistration.DownloadCount)
                    .Select(p =>
                    {
                        var viewModel = _listPackageItemViewModelFactory.Create(p, currentUser, false);
                        viewModel.DownloadCount = p.PackageRegistration.DownloadCount;
                        return viewModel;
                    }).ToList();
            }
            else
            {
                var packageResult = PackageService.FindPackagesByProfile(user, page, GalleryConstants.DefaultPackageListPageSize);

                packages = packageResult.Packages
                    .Select(p =>
                    {
                        var viewModel = _listPackageItemViewModelFactory.Create(p, currentUser, false);
                        viewModel.DownloadCount = p.PackageRegistration.DownloadCount;
                        return viewModel;
                    }).ToList();

                totalDownloadCount = packageResult.TotalDownloadCount;
                packageCount = packageResult.PackageCount;
            }

            var model = new UserProfileModel(
                user,
                currentUser,
                packages,
                page - 1,
                GalleryConstants.DefaultPackageListPageSize,
                Url,
                usesDatabasePaging,
                totalDownloadCount,
                packageCount);

            return View(model);

        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangePassword(UserAccountViewModel model)
        {
            var user = GetCurrentUser();

            var oldPassword = user.Credentials.FirstOrDefault(
                c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));

            if (oldPassword == null)
            {
                // User is requesting a password set email
                var resetResultType = await AuthenticationService.GeneratePasswordResetToken(user, GalleryConstants.PasswordResetTokenExpirationHours * 60);
                if (resetResultType == PasswordResetResultType.UserNotConfirmed)
                {
                    ModelState.AddModelError("ChangePassword", Strings.UserIsNotYetConfirmed);
                    return AccountView(user, model);
                }

                return await SendPasswordResetEmailAsync(user, forgotPassword: false);
            }
            else
            {
                if (model.ChangePassword.DisablePasswordLogin)
                {
                    return await RemovePassword();
                }

                if (!ModelState.IsValidField("ChangePassword"))
                {
                    return AccountView(user, model);
                }

                if (model.ChangePassword.NewPassword != model.ChangePassword.VerifyPassword)
                {
                    ModelState.AddModelError("ChangePassword.VerifyPassword", Strings.PasswordDoesNotMatch);
                    return AccountView(user, model);
                }

                if (!await AuthenticationService.ChangePassword(user, model.ChangePassword.OldPassword, model.ChangePassword.NewPassword))
                {
                    ModelState.AddModelError("ChangePassword.OldPassword", Strings.CurrentPasswordIncorrect);
                    return AccountView(user, model);
                }

                TempData["Message"] = Strings.PasswordChanged;
                return RedirectToAction("Account");
            }
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangeMultiFactorAuthentication(bool enableMultiFactor)
        {
            var user = GetCurrentUser();

            if (user.IsLocked && enableMultiFactor == false)
            {
                TempData["ErrorMessage"] = ServicesStrings.UserAccountIsLocked;
                return RedirectToAction(AccountAction);
            }

            var referrer = OwinContext.Request?.Headers?.Get("Referer") ?? "Unknown";

            await UserService.ChangeMultiFactorAuthentication(user, enableMultiFactor, referrer);

            TempData["Message"] = string.Format(
                enableMultiFactor ? Strings.MultiFactorAuth_Enabled : Strings.MultiFactorAuth_Disabled,
                _config.Brand);

            if (enableMultiFactor)
            {
                // Add the claim to remove the warning indicators for 2FA.
                OwinContext.AddClaim(NuGetClaims.EnabledMultiFactorAuthentication);
            }
            else
            {
                // Remove the claim from login to show warning indicators for 2FA.
                OwinContext.RemoveClaim(NuGetClaims.EnabledMultiFactorAuthentication);
            }

            return RedirectToAction(AccountAction);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> Send2FAFeedback(string feedback)
        {
            try
            {
                var user = GetCurrentUser();
                var sanitizedUserFeedback = HttpUtility.HtmlEncode(feedback);

                var message = new TwoFactorFeedbackMessage(MessageServiceConfiguration, sanitizedUserFeedback, user);
                await MessageService.SendMessageAsync(message);

                return Json(new { success = true });
            }
            catch (ArgumentException ex)
            {
                ex.Log();

                return Json(new { success = false, message = Strings.TwoFAFeedback_Error });
            }
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual Task<ActionResult> RemovePassword()
        {
            var user = GetCurrentUser();
            var passwordCred = user.Credentials.SingleOrDefault(
                c => c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));

            return RemoveCredentialInternal(user, passwordCred, Strings.PasswordRemoved);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> RemoveCredential(string credentialType, int? credentialKey)
        {
            var user = GetCurrentUser();
            var cred = user.Credentials.SingleOrDefault(
                c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase)
                    && CredentialKeyMatches(credentialKey, c));

            if (CredentialTypes.IsApiKey(credentialType))
            {
                return await RemoveApiKeyCredential(user, cred);
            }

            return await RemoveCredentialInternal(user, cred, Strings.CredentialRemoved);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> RevokeApiKeyCredential(string credentialType, int? credentialKey)
        {
            var user = GetCurrentUser();

            var cred = user.Credentials.SingleOrDefault(
                c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase)
                    && CredentialKeyMatches(credentialKey, c));

            if (cred == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return Json(Strings.CredentialNotFound);
            }

            await AuthenticationService.RevokeApiKeyCredential(cred, CredentialRevocationSource.User);

            var credViewModel = AuthenticationService.DescribeCredential(cred);

            var emailMessage = new ApiKeyRevokedMessage(
                _config,
                user,
                credViewModel.GetCredentialTypeInfo());
            await MessageService.SendMessageAsync(emailMessage);

            return Json(new ApiKeyViewModel(credViewModel));
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual ActionResult LinkOrChangeExternalCredential()
        {
            return Redirect(Url.AuthenticateExternal(Url.AccountSettings()));
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> RegenerateCredential(string credentialType, int? credentialKey)
        {
            var user = GetCurrentUser();

            if (user.IsLocked)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(ServicesStrings.UserAccountIsLocked);
            }

            var cred = user.Credentials.SingleOrDefault(
                c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase)
                    && CredentialKeyMatches(credentialKey, c));

            if (cred == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return Json(Strings.CredentialNotFound);
            }

            if (!cred.IsScopedApiKey())
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.Unsupported);
            }

            var newCredentialViewModel = await GenerateApiKeyInternal(
                cred.Description,
                BuildScopes(cred.Scopes),
                cred.ExpirationTicks.HasValue
                    ? new TimeSpan(cred.ExpirationTicks.Value) : new TimeSpan?());

            await AuthenticationService.RemoveCredential(user, cred);

            return Json(new ApiKeyViewModel(newCredentialViewModel));
        }

        private static bool CredentialKeyMatches(int? credentialKey, Credential c)
        {
            return (credentialKey == null || credentialKey == 0 || c.Key == credentialKey);
        }

        [HttpGet]
        public virtual ActionResult PasswordChanged()
        {
            return View();
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult TrustedPublishing()
        {
            User currentUser = GetCurrentUser();
            if (!_featureFlagService.IsTrustedPublishingEnabled(currentUser))
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var userPolicies = _federatedCredentialService.GetPoliciesCreatedByUser(currentUser.Key);

            // Show newest policies on the top.
            var policies = userPolicies
                .OrderByDescending(p => p.Created)
                .Select(p => CreatePublisherViewModel(currentUser, p))
                .Where(p => p != null)
                .ToArray();

            // Get package owners (user's self or organizations). The final list can be empty,
            // e.g. if the user is not a member of any organization and has no packages.
            var ownersCandidates = new List<User>() { currentUser };
            ownersCandidates.AddRange(currentUser.Organizations.Select(o => o.Organization));
            var owners = ownersCandidates
                .Where(o => _federatedCredentialService.IsValidPolicyOwner(currentUser, o))
                .ToArray();

            var model = new TrustedPublisherPolicyListViewModel
            {
                Username = currentUser.Username,
                Policies = policies,
                PackageOwners = owners.Select(o => o.Username).ToArray(),
            };

            return View("TrustedPublishing", model);
        }

        private TrustedPublisherPolicyViewModel CreatePublisherViewModel(User currentUser, FederatedCredentialPolicy policy)
        {
            // Currently only GitHub Actions policies are supported by our Trusted Publishing UX.
            if (policy.Type != FederatedCredentialType.GitHubActions)
            {
                return null;
            }

            if (GitHubPolicyDetailsViewModel.FromDatabaseJson(policy.Criteria)
                is not GitHubPolicyDetailsViewModel policyDetails)
            {
                return null;
            }

            var owner = currentUser.Key == policy.PackageOwnerUserKey ?
                currentUser : UserService.FindByKey(policy.PackageOwnerUserKey);

            bool isOwnerValid = _federatedCredentialService.IsValidPolicyOwner(currentUser, owner);

            return new TrustedPublisherPolicyViewModel
            {
                Key = policy.Key,
                PolicyName = !string.IsNullOrEmpty(policy.PolicyName) ? policy.PolicyName : $"Policy {policy.Key}",
                Owner = policy.PackageOwner.Username,
                IsOwnerValid = isOwnerValid,
                PolicyDetails = policyDetails
            };
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> GenerateTrustedPublisherPolicy(string policyName, string owner, string criteria)
        {
            User currentUser = GetCurrentUser();
            if (!_featureFlagService.IsTrustedPublishingEnabled(currentUser))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.DefaultUserSafeExceptionMessage);
            }

            if (VerifyPolicyName(policyName) is string policyNameError)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(policyNameError);
            }

            if (policyName.Length > FederatedCredentialPolicy.MaxPolicyNameLength)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.TrustedPublisher_NameTooLong);
            }

            if (string.IsNullOrWhiteSpace(owner))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.TrustedPublisher_PolicyOwnerRequired);
            }

            if (currentUser.IsLocked)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(ServicesStrings.UserAccountIsLocked);
            }

            if (UserService.FindByUsername(owner) is not User policyOwner)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.AddOwner_OwnerNotFound);
            }

            // Currently only GitHub Actions policies are expected
            if (GitHubPolicyDetailsViewModel.FromViewJson(criteria)
                is not GitHubPolicyDetailsViewModel policyDetails)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.TrustedPublisher_Unexpected);
            }

            if (policyDetails.Validate() is string errorMessage && errorMessage.Length > 0)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(errorMessage);
            }

            // Try adding policy
            string dbJson = policyDetails.ToDatabaseJson();
            AddFederatedCredentialPolicyResult result = await _federatedCredentialService.AddTrustedPublishingPolicyAsync(
                currentUser, policyOwner, policyName,
                policyDetails.PublisherType, dbJson);

            switch (result.Type)
            {
                case AddFederatedCredentialPolicyResultType.BadRequest:
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return Json(result.UserMessage);

                case AddFederatedCredentialPolicyResultType.Unauthorized:
                    Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return Json(result.UserMessage);

                case AddFederatedCredentialPolicyResultType.Created:
                    var model = new TrustedPublisherPolicyViewModel
                    {
                        Key = result.Policy.Key,
                        PolicyName = policyName,
                        Owner = owner,
                        IsOwnerValid = true, // we already validated owner above
                        PolicyDetails = policyDetails
                    };
                    return Json(model);

                default:
                    throw new NotImplementedException($"The policy creation result type '{result.Type}' is not supported.");
            }
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> EditTrustedPublisherPolicy(int? federatedCredentialKey, string policyName, string criteria)
        {
            User currentUser = GetCurrentUser();
            if (!_featureFlagService.IsTrustedPublishingEnabled(currentUser))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.DefaultUserSafeExceptionMessage);
            }

            if (VerifyPolicyName(policyName) is string policyNameError)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(policyNameError);
            }

            var result = GetFederatedCredentialPolicy(federatedCredentialKey);
            if (result.policy == null)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(result.error);
            }

            if (CreatePublisherViewModel(currentUser, result.policy) is not TrustedPublisherPolicyViewModel model ||
                !model.IsOwnerValid)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.TrustedPublisher_Unexpected);
            }

            model.PolicyName = policyName;
            var newDetails = model.PolicyDetails.Update(criteria);
            if (newDetails.Validate() is string errorMessage && errorMessage.Length > 0)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(errorMessage);
            }

            await _federatedCredentialService.UpdatePolicyAsync(result.policy, model.PolicyName, newDetails.ToDatabaseJson());
            model.PolicyDetails = newDetails;

            return Json(model);
        }

        private static string VerifyPolicyName(string policyName)
        {
            if (string.IsNullOrWhiteSpace(policyName))
            {
                return Strings.TrustedPublisher_PolicyNameRequired;
            }
            if (policyName.Length > FederatedCredentialPolicy.MaxPolicyNameLength)
            {
                return Strings.TrustedPublisher_NameTooLong;
            }
            return null;
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> EnableTrustedPublisherPolicy(int? federatedCredentialKey)
        {
            User currentUser = GetCurrentUser();
            if (!_featureFlagService.IsTrustedPublishingEnabled(currentUser))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.DefaultUserSafeExceptionMessage);
            }

            var result = GetFederatedCredentialPolicy(federatedCredentialKey);
            if (result.policy == null)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(result.error);
            }

            if (CreatePublisherViewModel(currentUser, result.policy) is not TrustedPublisherPolicyViewModel model ||
                !model.IsOwnerValid ||
                model.PolicyDetails is not GitHubPolicyDetailsViewModel gitHubModel)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.TrustedPublisher_Unexpected);
            }

            if (!gitHubModel.IsPermanentlyEnabled)
            {
                gitHubModel.InitializeValidateByDate();
                result.policy.Criteria = gitHubModel.ToDatabaseJson();
                await _federatedCredentialService.UpdatePolicyAsync(result.policy, result.policy.PolicyName, gitHubModel.ToDatabaseJson());
            }

            return Json(model);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> RemoveTrustedPublisherPolicy(int? federatedCredentialKey)
        {
            User currentUser = GetCurrentUser();
            if (!_featureFlagService.IsTrustedPublishingEnabled(currentUser))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.DefaultUserSafeExceptionMessage);
            }

            var result = GetFederatedCredentialPolicy(federatedCredentialKey);
            if (result.policy == null)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(result.error);
            }

            await _federatedCredentialService.DeletePolicyAsync(result.policy);
            return Json(Strings.TrustedPolicyRemoved);
        }

        private (FederatedCredentialPolicy policy, string error) GetFederatedCredentialPolicy(int? federatedCredentialKey)
        {
            if (federatedCredentialKey is not int key ||
                _federatedCredentialService.GetPolicyByKey(key) is not FederatedCredentialPolicy policy)
            {
                return (null, Strings.TrustedPublisher_Unexpected);
            }

            // Sanity check. The policy must be created by current user
            var currentUser = GetCurrentUser();
            if (currentUser.Key != policy.CreatedByUserKey)
            {
                return (null, Strings.Unauthorized);
            }

            return (policy, null);
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> GenerateApiKey(string description, string owner, string[] scopes = null, string[] subjects = null, int? expirationInDays = null)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.ApiKeyDescriptionRequired);
            }
            if (string.IsNullOrWhiteSpace(owner))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.ApiKeyOwnerRequired);
            }

            var currentUser = GetCurrentUser();
            if (currentUser.IsLocked)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(ServicesStrings.UserAccountIsLocked);
            }

            // Get the owner scope
            User scopeOwner = UserService.FindByUsername(owner);
            if (scopeOwner == null)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.UserNotFound);
            }

            var resolvedScopes = _credentialBuilder.BuildScopes(scopeOwner, scopes, subjects);
            if (!_credentialBuilder.VerifyScopes(GetCurrentUser(), resolvedScopes))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.ApiKeyScopesNotAllowed);
            }

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

            var newCredentialViewModel = await GenerateApiKeyInternal(description, resolvedScopes, expiration);

            var emailMessage = new CredentialAddedMessage(
                    _config,
                    currentUser,
                    newCredentialViewModel.GetCredentialTypeInfo());
            await MessageService.SendMessageAsync(emailMessage);

            return Json(new ApiKeyViewModel(newCredentialViewModel));
        }

        [UIAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> EditCredential(string credentialType, int? credentialKey, string[] subjects)
        {
            var user = GetCurrentUser();
            var cred = user.Credentials.SingleOrDefault(
                c => string.Equals(c.Type, credentialType, StringComparison.OrdinalIgnoreCase)
                    && CredentialKeyMatches(credentialKey, c));

            if (cred == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return Json(Strings.CredentialNotFound);
            }

            if (!cred.IsScopedApiKey())
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.Unsupported);
            }

            var scopeOwner = cred.Scopes.GetOwnerScope();
            var scopes = cred.Scopes.Select(x => x.AllowedAction).Distinct().ToArray();
            var newScopes = _credentialBuilder.BuildScopes(scopeOwner, scopes, subjects);

            await AuthenticationService.EditCredentialScopes(user, cred, newScopes);

            var credentialViewModel = AuthenticationService.DescribeCredential(cred);

            return Json(new ApiKeyViewModel(credentialViewModel));
        }

        protected override RouteUrlTemplate<string> GetDeleteCertificateForAccountTemplate(string accountName)
        {
            return Url.DeleteUserCertificateTemplate();
        }

        private async Task<CredentialViewModel> GenerateApiKeyInternal(string description, ICollection<Scope> scopes, TimeSpan? expiration)
        {
            var user = GetCurrentUser();

            // Create a new API Key credential, and save to the database
            var newCredential = _credentialBuilder.CreateApiKey(expiration, out string plaintextApiKey);
            newCredential.Description = description;
            newCredential.Scopes = scopes;
            newCredential.WasCreatedSecurely = User.WasMultiFactorAuthenticated();

            await AuthenticationService.AddCredential(user, newCredential);

            var credentialViewModel = AuthenticationService.DescribeCredential(newCredential);
            credentialViewModel.Value = plaintextApiKey;

            return credentialViewModel;
        }

        private static IList<Scope> BuildScopes(IEnumerable<Scope> scopes)
        {
            return scopes.Select(scope => new Scope(scope.Owner, scope.Subject, scope.AllowedAction)).ToList();
        }

        private async Task<JsonResult> RemoveApiKeyCredential(User user, Credential cred)
        {
            if (cred == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return Json(Strings.CredentialNotFound);
            }

            var credentialTypeInfo = AuthenticationService.DescribeCredential(cred).GetCredentialTypeInfo();

            await AuthenticationService.RemoveCredential(user, cred);

            // Notify the user of the change
            var emailMessage = new CredentialRemovedMessage(
                _config,
                user,
                credentialTypeInfo);
            await MessageService.SendMessageAsync(emailMessage);

            return Json(Strings.CredentialRemoved);
        }

        private async Task<ActionResult> RemoveCredentialInternal(User user, Credential cred, string message)
        {
            if (cred == null)
            {
                TempData["Message"] = Strings.CredentialNotFound;

                return RedirectToAction("Account");
            }

            // Count credentials and make sure the user can always login
            if (!cred.IsApiKey() && CountLoginCredentials(user) <= 1)
            {
                TempData["Message"] = Strings.CannotRemoveOnlyLoginCredential;
            }
            else
            {
                await AuthenticationService.RemoveCredential(user, cred);

                if (cred.IsPassword())
                {
                    // Clear the password login claim, to remove warnings.
                    OwinContext.RemoveClaim(NuGetClaims.PasswordLogin);
                }

                // Notify the user of the change
                var emailMessage = new CredentialRemovedMessage(
                    _config,
                    user,
                    AuthenticationService.DescribeCredential(cred).GetCredentialTypeInfo());
                await MessageService.SendMessageAsync(emailMessage);

                TempData["Message"] = message;
            }

            return RedirectToAction("Account");
        }

        protected override void UpdateAccountViewModel(User account, UserAccountViewModel model)
        {
            base.UpdateAccountViewModel(account, model);

            model.CredentialGroups = GetCredentialGroups(account);
            model.SignInCredentialCount = model
                .CredentialGroups
                .Where(p => p.Key == CredentialKind.Password || p.Key == CredentialKind.External)
                .Sum(p => p.Value.Count);

            model.ChangePassword = model.ChangePassword ?? new ChangePasswordViewModel();
            model.ChangePassword.DisablePasswordLogin = !model.HasPassword;
        }

        private Dictionary<CredentialKind, List<CredentialViewModel>> GetCredentialGroups(User user)
        {
            return user
                .Credentials
                .Where(CredentialTypes.IsViewSupportedCredential)
                .OrderByDescending(c => c.Created)
                .ThenBy(c => c.Description)
                .Select(AuthenticationService.DescribeCredential)
                .GroupBy(c => c.Kind)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static int CountLoginCredentials(User user)
        {
            return user.Credentials.Count(c =>
                c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase) ||
                c.Type.StartsWith(CredentialTypes.External.Prefix, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<ActionResult> SendPasswordResetEmailAsync(User user, bool forgotPassword)
        {
            var resetPasswordUrl = Url.ResetEmailOrPassword(
                user.Username,
                user.PasswordResetToken,
                forgotPassword,
                relativeUrl: false,
                supportEmail: true);

            var message = new PasswordResetInstructionsMessage(
                MessageServiceConfiguration,
                user,
                resetPasswordUrl,
                forgotPassword);

            await MessageService.SendMessageAsync(message);

            return RedirectToAction(actionName: "PasswordSent", controllerName: "Users");
        }

        private OwnerRequestsViewModel CreateOwnerRequestsViewModel(IEnumerable<PackageOwnerRequest> received, IEnumerable<PackageOwnerRequest> sent, User currentUser)
        {
            var viewModel = new OwnerRequestsViewModel
            {
                Received = new OwnerRequestsListViewModel
                {
                    Requests = received.Select(r => CreateOwnerRequestsListItemViewModel(r, currentUser)).ToList()
                },
                Sent = new OwnerRequestsListViewModel
                {
                    Requests = sent.Select(r => CreateOwnerRequestsListItemViewModel(r, currentUser)).ToList()
                },
            };

            return viewModel;
        }

        private OwnerRequestsListItemViewModel CreateOwnerRequestsListItemViewModel(PackageOwnerRequest request, User currentUser)
        {
            var package = PackageService.FindPackageByIdAndVersion(request.PackageRegistration.Id, version: null, semVerLevelKey: SemVerLevelKey.SemVer2, allowPrerelease: true);
            var packageViewModel = _listPackageItemViewModelFactory.Create(package, currentUser);

            return new OwnerRequestsListItemViewModel
            {
                Request = request,
                Package = packageViewModel,
                CanAccept = ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(currentUser, request.NewOwner) == PermissionsCheckResult.Allowed,
                CanCancel = packageViewModel.CanManageOwners,
            };
        }
    }
}
