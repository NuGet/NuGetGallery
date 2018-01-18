// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
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
        private readonly IPackageOwnerRequestService _packageOwnerRequestService;
        private readonly IAppConfiguration _config;
        private readonly AuthenticationService _authService;
        private readonly ICredentialBuilder _credentialBuilder;
        private readonly IDeleteAccountService _deleteAccountService;
        private readonly ISupportRequestService _supportRequestService;

        public UsersController(
            ICuratedFeedService feedsQuery,
            IUserService userService,
            IPackageService packageService,
            IPackageOwnerRequestService packageOwnerRequestService,
            IMessageService messageService,
            IAppConfiguration config,
            AuthenticationService authService,
            ICredentialBuilder credentialBuilder,
            IDeleteAccountService deleteAccountService,
            ISupportRequestService supportRequestService)
        {
            _curatedFeedService = feedsQuery ?? throw new ArgumentNullException(nameof(feedsQuery));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageOwnerRequestService = packageOwnerRequestService ?? throw new ArgumentNullException(nameof(packageOwnerRequestService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _deleteAccountService = deleteAccountService ?? throw new ArgumentNullException(nameof(deleteAccountService));
            _supportRequestService = supportRequestService ?? throw new ArgumentNullException(nameof(supportRequestService));
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult ConfirmationRequired()
        {
            var model = new ConfirmationViewModel(GetCurrentUser());
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ActionName("ConfirmationRequired")]
        [ValidateAntiForgeryToken]
        public virtual ActionResult ConfirmationRequiredPost()
        {
            User user = GetCurrentUser();
            var confirmationUrl = Url.ConfirmEmail(user.Username, user.EmailConfirmationToken, relativeUrl: false);

            var alreadyConfirmed = user.UnconfirmedEmailAddress == null;

            ConfirmationViewModel model;
            if (!alreadyConfirmed)
            {
                _messageService.SendNewAccountEmail(new MailAddress(user.UnconfirmedEmailAddress, user.Username), confirmationUrl);

                model = new ConfirmationViewModel(user)
                {
                    SentEmail = true
                };
            }
            else
            {
                model = new ConfirmationViewModel(user);
            }
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult Account()
        {
            return AccountView(new AccountViewModel());
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult DeleteRequest()
        {
            var user = GetCurrentUser();

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound("User not found.");
            }

            var listPackageItems = _packageService
                 .FindPackagesByAnyMatchingOwner(user, includeUnlisted: true)
                 .Select(p => new ListPackageItemViewModel(p, user))
                 .ToList();

            bool hasPendingRequest = _supportRequestService.GetIssues().Where((issue)=> (issue.UserKey.HasValue && issue.UserKey.Value == user.Key) && 
                                                                                 string.Equals(issue.IssueTitle, Strings.AccountDelete_SupportRequestTitle) &&
                                                                                 issue.Key != IssueStatusKeys.Resolved).Any();

            var model = new DeleteAccountViewModel()
            {
                Packages = listPackageItems,
                User = user,
                AccountName = user.Username,
                HasPendingRequests = hasPendingRequest
            };
            
            return View("DeleteAccount", model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> RequestAccountDeletion()
        {
            var user = GetCurrentUser();

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound("User not found.");
            }

            var supportRequest = await _supportRequestService.AddNewSupportRequestAsync(
                Strings.AccountDelete_SupportRequestTitle,
                Strings.AccountDelete_SupportRequestTitle,
                user.EmailAddress,
                "The user requested to have the account deleted.",
                user);
            var isSupportRequestCreated = supportRequest != null;
            if (!isSupportRequestCreated)
            {
                TempData["RequestFailedMessage"] = Strings.AccountDelete_CreateSupportRequestFails;
                return RedirectToAction("DeleteRequest");
            }
            _messageService.SendAccountDeleteNotice(user.ToMailAddress(), user.Username);
           
            return RedirectToAction("DeleteRequest");
        }

        [HttpGet]
        [Authorize(Roles = "Admins")]
        public virtual ActionResult Delete(string accountName)
        {
            var user = _userService.FindByUsername(accountName);
            if(user == null || user.IsDeleted || (user is Organization))
            {
                return HttpNotFound("User not found.");
            }

            var listPackageItems = _packageService
                 .FindPackagesByAnyMatchingOwner(user, includeUnlisted:true)
                 .Select(p => new ListPackageItemViewModel(p, user))
                 .ToList();
            var model = new DeleteUserAccountViewModel
            {
                Packages = listPackageItems,
                User = user,
                AccountName = user.Username,
            };
            return View("DeleteUserAccount", model);
        }

        [HttpDelete]
        [Authorize(Roles = "Admins")]
        [RequiresAccountConfirmation("Delete account")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Delete(DeleteUserAccountViewModel model)
        {
            var user = _userService.FindByUsername(model.AccountName);
            if (user == null || user.IsDeleted)
            {
                return View("DeleteUserAccountStatus", new DeleteUserAccountStatus()
                {
                    AccountName = model.AccountName,
                    Description = $"Account {model.AccountName} not found.",
                    Success = false
                });
            }
            else
            {
                var admin = GetCurrentUser();
                var status = await _deleteAccountService.DeleteGalleryUserAccountAsync(user, admin, model.Signature, model.ShouldUnlist, commitAsTransaction: true);
                return View("DeleteUserAccountStatus", status);
            }
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult ApiKeys()
        {
            var user = GetCurrentUser();

            // Get API keys
            if (!GetCredentialGroups(user).TryGetValue(CredentialKind.Token, out List<CredentialViewModel> credentials))
            {
                credentials = new List<CredentialViewModel>();
            }

            var apiKeys = credentials
                .Select(c => new ApiKeyViewModel(c))
                .ToList();

            // Get package owners (user's self or organizations)
            var owners = user.Organizations
                .Select(o => CreateApiKeyOwnerViewModel(
                    o.Organization,
                    // todo: move logic for canPushNew to PermissionsService
                    canPushNew: o.IsAdmin)
                    ).ToList();
            owners.Insert(0, CreateApiKeyOwnerViewModel(user, canPushNew: true));

            var model = new ApiKeyListViewModel
            {
                ApiKeys = apiKeys,
                ExpirationInDaysForApiKeyV1 = _config.ExpirationInDaysForApiKeyV1,
                PackageOwners = owners,
            };

            return View("ApiKeys", model);
        }

        private ApiKeyOwnerViewModel CreateApiKeyOwnerViewModel(User user, bool canPushNew)
        {
            return new ApiKeyOwnerViewModel(
                user.Username,
                canPushNew,
                packageIds: _packageService.FindPackageRegistrationsByOwner(user)
                                .Select(p => p.Id)
                                .OrderBy(i => i)
                                .ToList());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ChangeEmailSubscription(AccountViewModel model)
        {
            var user = GetCurrentUser();

            await _userService.ChangeEmailSubscriptionAsync(
                user, 
                model.ChangeNotifications.EmailAllowed, 
                model.ChangeNotifications.NotifyPackagePushed);

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
            var currentUser = GetCurrentUser();

            var owners = new List<ListPackageOwnerViewModel> {
                new ListPackageOwnerViewModel
                {
                    Username = "All packages"
                },
                new ListPackageOwnerViewModel(currentUser)
            }.Concat(currentUser.Organizations.Select(o => new ListPackageOwnerViewModel(o.Organization)));

            var packages = _packageService.FindPackagesByAnyMatchingOwner(currentUser, includeUnlisted: true);
            var listedPackages = packages
                .Where(p => p.Listed)
                .Select(p => new ListPackageItemViewModel(p, currentUser)).OrderBy(p => p.Id)
                .ToList();
            var unlistedPackages = packages
                .Where(p => !p.Listed)
                .Select(p => new ListPackageItemViewModel(p, currentUser)).OrderBy(p => p.Id)
                .ToList();

            var incoming = _packageOwnerRequestService.GetPackageOwnershipRequests(newOwner: currentUser);
            var outgoing = _packageOwnerRequestService.GetPackageOwnershipRequests(requestingOwner: currentUser);

            var ownerRequests = new OwnerRequestsViewModel(incoming, outgoing, currentUser, _packageService);
            var reservedPrefixes = new ReservedNamespaceListViewModel(currentUser.ReservedNamespaces);

            var model = new ManagePackagesViewModel
            {
                Owners = owners,
                ListedPackages = listedPackages,
                UnlistedPackages = unlistedPackages,
                OwnerRequests = ownerRequests,
                ReservedNamespaces = reservedPrefixes
            };
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public virtual ActionResult Organizations()
        {
            var currentUser = GetCurrentUser();

            var model = new ListOrganizationsViewModel(currentUser, _packageService);

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
                var result = await _authService.GeneratePasswordResetToken(model.Email, Constants.PasswordResetTokenExpirationHours * 60);
                switch (result.Type)
                {
                    case PasswordResetResultType.UserNotConfirmed:
                        ModelState.AddModelError("Email", Strings.UserIsNotYetConfirmed);
                        break;
                    case PasswordResetResultType.UserNotFound:
                        ModelState.AddModelError("Email", Strings.CouldNotFindAnyoneWithThatUsernameOrEmail);
                        break;
                    case PasswordResetResultType.Success:
                        return SendPasswordResetEmail(result.User, forgotPassword: true);
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
                _messageService.SendCredentialAddedNotice(credential.User, _authService.DescribeCredential(credential));
            }

            return RedirectToAction(
                actionName: "PasswordChanged",
                controllerName: "Users");
        }

        [Authorize]
        public virtual async Task<ActionResult> Confirm(string username, string token)
        {
            // We don't want Login to go to this page as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            var user = GetCurrentUser();

            if (!String.Equals(username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View(new ConfirmationViewModel(user)
                {
                    WrongUsername = true,
                    SuccessfulConfirmation = false,
                });
            }

            string existingEmail = user.EmailAddress;
            var model = new ConfirmationViewModel(user);

            if (!model.AlreadyConfirmed)
            {
                try
                {
                    model.SuccessfulConfirmation = await _userService.ConfirmEmailAddress(user, token);
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
        public virtual ActionResult Profiles(string username, int page = 1)
        {
            var user = _userService.FindByUsername(username);
            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }

            var packages = _packageService.FindPackagesByAnyMatchingOwner(user, includeUnlisted: false)
                .OrderByDescending(p => p.PackageRegistration.DownloadCount)
                .Select(p => new ListPackageItemViewModel(p, user)
                {
                    DownloadCount = p.PackageRegistration.DownloadCount
                }).ToList();

            var model = new UserProfileModel(user, packages, page - 1, Constants.DefaultPackageListPageSize, Url);

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
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

                Credential _;

                if (!_authService.ValidatePasswordCredential(user.Credentials, model.ChangeEmail.Password, out _))
                {
                    ModelState.AddModelError("ChangeEmail.Password", Strings.CurrentPasswordIncorrect);
                    return AccountView(model);
                }
            }

            // No password? We can't do any additional verification...

            if (string.Equals(model.ChangeEmail.NewEmail, user.LastSavedEmailAddress, StringComparison.OrdinalIgnoreCase))
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
                var confirmationUrl = Url.ConfirmEmail(user.Username, user.EmailConfirmationToken, relativeUrl: false);
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
        [ValidateAntiForgeryToken]
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
                var resetResultType = await _authService.GeneratePasswordResetToken(user, Constants.PasswordResetTokenExpirationHours * 60);
                if (resetResultType == PasswordResetResultType.UserNotConfirmed)
                {
                    ModelState.AddModelError("ChangePassword", Strings.UserIsNotYetConfirmed);
                    return AccountView(model);
                }

                return SendPasswordResetEmail(user, forgotPassword: false);
            }
            else
            {
                if (!model.ChangePassword.EnablePasswordLogin)
                {
                    return await RemovePassword();
                }

                if (!ModelState.IsValidField("ChangePassword"))
                {
                    return AccountView(model);
                }

                if (model.ChangePassword.NewPassword != model.ChangePassword.VerifyPassword)
                {
                    ModelState.AddModelError("ChangePassword.VerifyPassword", Strings.PasswordDoesNotMatch);
                    return AccountView(model);
                }

                if (!await _authService.ChangePassword(user, model.ChangePassword.OldPassword, model.ChangePassword.NewPassword))
                {
                    ModelState.AddModelError("ChangePassword.OldPassword", Strings.CurrentPasswordIncorrect);
                    return AccountView(model);
                }

                TempData["Message"] = Strings.PasswordChanged;
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

            return RemoveCredentialInternal(user, passwordCred, Strings.PasswordRemoved);
        }

        [HttpPost]
        [Authorize]
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
        [Authorize]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> RegenerateCredential(string credentialType, int? credentialKey)
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
           
            var newCredentialViewModel = await GenerateApiKeyInternal(
                cred.Description,
                BuildScopes(cred.Scopes),
                cred.ExpirationTicks.HasValue
                    ? new TimeSpan(cred.ExpirationTicks.Value) : new TimeSpan?());

            await _authService.RemoveCredential(user, cred);

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

        [Authorize]
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

            // Get the owner scope
            User scopeOwner = _userService.FindByUsername(owner);
            if (scopeOwner == null)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(Strings.UserNotFound);
            }

            // todo: move validation logic to PermissionsService
            var resolvedScopes = BuildScopes(scopeOwner, scopes, subjects);
            if (!VerifyScopes(scopeOwner, resolvedScopes))
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

            _messageService.SendCredentialAddedNotice(GetCurrentUser(), newCredentialViewModel);

            return Json(new ApiKeyViewModel(newCredentialViewModel));
        }

        [Authorize]
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
            var newScopes = BuildScopes(scopeOwner, scopes, subjects);

            await _authService.EditCredentialScopes(user, cred, newScopes);

            var credentialViewModel = _authService.DescribeCredential(cred);

            return Json(new ApiKeyViewModel(credentialViewModel));
        }

        private async Task<CredentialViewModel> GenerateApiKeyInternal(string description, ICollection<Scope> scopes, TimeSpan? expiration)
        {
            var user = GetCurrentUser();

            // Create a new API Key credential, and save to the database
            var newCredential = _credentialBuilder.CreateApiKey(expiration, out string plaintextApiKey);
            newCredential.Description = description;
            newCredential.Scopes = scopes;

            await _authService.AddCredential(user, newCredential);

            var credentialViewModel = _authService.DescribeCredential(newCredential);
            credentialViewModel.Value = plaintextApiKey;

            return credentialViewModel;
        }

        // todo: integrate verification logic into PermissionsService.
        private bool VerifyScopes(User scopeOwner, IEnumerable<Scope> scopes)
        {
            var currentUser = GetCurrentUser();

            // scoped to the user
            if (currentUser.MatchesUser(scopeOwner))
            {
                return true;
            }
            // scoped to the user's organization
            else
            {
                var organization = currentUser.Organizations
                    .Where(o => o.Organization.MatchesUser(scopeOwner))
                    .FirstOrDefault();
                if (organization != null)
                {
                    return organization.IsAdmin || !scopes.Any(s => s.AllowsActions(NuGetScopes.PackagePush));
                }
            }

            return false;
        }

        private IList<Scope> BuildScopes(User scopeOwner, string[] scopes, string[] subjects)
        {
            var result = new List<Scope>();

            var subjectsList = subjects?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

            // No package filtering information was provided. So allow any pattern.
            if (!subjectsList.Any())
            {
                subjectsList.Add(NuGetPackagePattern.AllInclusivePattern);
            }

            if (scopes != null)
            {
                foreach (var scope in scopes)
                {
                    result.AddRange(subjectsList.Select(subject => new Scope(scopeOwner, subject, scope)));
                }
            }
            else
            {
                result.AddRange(subjectsList.Select(subject => new Scope(scopeOwner, subject, NuGetScopes.All)));
            }

            return result;
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

            await _authService.RemoveCredential(user, cred);

            // Notify the user of the change
            _messageService.SendCredentialRemovedNotice(user, _authService.DescribeCredential(cred));

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
            if (!CredentialTypes.IsApiKey(cred.Type) && CountLoginCredentials(user) <= 1)
            {
                TempData["Message"] = Strings.CannotRemoveOnlyLoginCredential;
            }
            else
            {
                await _authService.RemoveCredential(user, cred);

                // Notify the user of the change
                _messageService.SendCredentialRemovedNotice(user, _authService.DescribeCredential(cred));

                TempData["Message"] = message;
            }

            return RedirectToAction("Account");
        }

        private ActionResult AccountView(AccountViewModel model)
        {
            var user = GetCurrentUser();

            model.CuratedFeeds = _curatedFeedService
                .GetFeedsForManager(user.Key)
                .Select(f => f.Name)
                .ToList();
            model.CredentialGroups = GetCredentialGroups(user);
            model.SignInCredentialCount = model
                .CredentialGroups
                .Where(p => p.Key == CredentialKind.Password || p.Key == CredentialKind.External)
                .Sum(p => p.Value.Count);

            model.ExpirationInDaysForApiKeyV1 = _config.ExpirationInDaysForApiKeyV1;
            model.HasPassword = model.CredentialGroups.ContainsKey(CredentialKind.Password);
            model.CurrentEmailAddress = user.UnconfirmedEmailAddress ?? user.EmailAddress;
            model.HasConfirmedEmailAddress = !string.IsNullOrEmpty(user.EmailAddress);
            model.HasUnconfirmedEmailAddress = !string.IsNullOrEmpty(user.UnconfirmedEmailAddress);

            model.ChangePassword = model.ChangePassword ?? new ChangePasswordViewModel();
            model.ChangePassword.EnablePasswordLogin = model.HasPassword;

            model.ChangeNotifications = model.ChangeNotifications ?? new ChangeNotificationsViewModel();
            model.ChangeNotifications.EmailAllowed = user.EmailAllowed;
            model.ChangeNotifications.NotifyPackagePushed = user.NotifyPackagePushed;
           
            return View("Account", model);
        }

        private Dictionary<CredentialKind, List<CredentialViewModel>> GetCredentialGroups(User user)
        {
            return user
                .Credentials
                .Where(CredentialTypes.IsViewSupportedCredential)
                .OrderByDescending(c => c.Created)
                .ThenBy(c => c.Description)
                .Select(_authService.DescribeCredential)
                .GroupBy(c => c.Kind)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static int CountLoginCredentials(User user)
        {
            return user.Credentials.Count(c =>
                c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase) ||
                c.Type.StartsWith(CredentialTypes.ExternalPrefix, StringComparison.OrdinalIgnoreCase));
        }

        private ActionResult SendPasswordResetEmail(User user, bool forgotPassword)
        {
            var resetPasswordUrl = Url.ResetEmailOrPassword(
                user.Username,
                user.PasswordResetToken,
                forgotPassword,
                relativeUrl: false);
            _messageService.SendPasswordResetInstructions(user, resetPasswordUrl, forgotPassword);

            return RedirectToAction(actionName: "PasswordSent", controllerName: "Users");
        }
    }
}
