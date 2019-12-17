// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.AzureActiveDirectoryV2;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public static class AuthenticationFailureErrors
    {
        public const string ACCESSS_DENIED = "access_denied";
        public const string CONSENT_REQUIRED = "consent_required";
    }

    public partial class AuthenticationController
        : AppController
    {
        private readonly AuthenticationService _authService;

        private readonly IUserService _userService;

        private readonly IMessageService _messageService;

        private readonly ICredentialBuilder _credentialBuilder;

        private readonly IContentObjectService _contentObjectService;
        private readonly IMessageServiceConfiguration _messageServiceConfiguration;
        private const string EMAIL_FORMAT_PADDING = "**********";

        // Prioritize the external authentication mechanism.
        private static readonly string[] ExternalAuthenticationPriority = new string[] {
            Authenticator.GetName(typeof(AzureActiveDirectoryV2Authenticator)),
            Authenticator.GetName(typeof(MicrosoftAccountAuthenticator))
        };

        public AuthenticationController(
            AuthenticationService authService,
            IUserService userService,
            IMessageService messageService,
            ICredentialBuilder credentialBuilder,
            IContentObjectService contentObjectService,
            IMessageServiceConfiguration messageServiceConfiguration)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _messageServiceConfiguration = messageServiceConfiguration ?? throw new ArgumentNullException(nameof(messageServiceConfiguration));
        }

        /// <summary>
        /// Sign In\Register view
        /// </summary>
        [HttpGet]
        public virtual ActionResult LogOn(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = returnUrl;

            if (TempData.ContainsKey(GalleryConstants.ReturnUrlMessageViewDataKey))
            {
                ViewData[GalleryConstants.ReturnUrlMessageViewDataKey] = TempData[GalleryConstants.ReturnUrlMessageViewDataKey];
            }

            if (Request.IsAuthenticated)
            {
                return LoggedInRedirect(returnUrl);
            }

            return SignInView(new LogOnViewModel());
        }

        /// <summary>
        /// Sign In NuGet account view
        /// </summary>
        [HttpGet]
        public virtual ActionResult LogOnNuGetAccount(string returnUrl)
        {
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                return LoggedInRedirect(returnUrl);
            }

            return SignInNuGetAccountView(new LogOnViewModel());
        }

        /// </summary>
        [HttpGet]
        public virtual ActionResult SignUp(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                return LoggedInRedirect(returnUrl);
            }

            return RegisterView(new LogOnViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> SignIn(LogOnViewModel model, string returnUrl, bool linkingAccount)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                return LoggedInRedirect(returnUrl);
            }

            if (!ModelState.IsValid)
            {
                return SignInOrExternalLinkView(model, linkingAccount);
            }

            var authenticationResult = await _authService.Authenticate(model.SignIn.UserNameOrEmail, model.SignIn.Password);

            if (authenticationResult.Result != PasswordAuthenticationResult.AuthenticationResult.Success)
            {
                string modelErrorMessage = string.Empty;

                if (authenticationResult.Result == PasswordAuthenticationResult.AuthenticationResult.BadCredentials)
                {
                    modelErrorMessage = Strings.UsernameAndPasswordNotFound;
                }
                else if (authenticationResult.Result == PasswordAuthenticationResult.AuthenticationResult.AccountLocked)
                {
                    string timeRemaining =
                        authenticationResult.LockTimeRemainingMinutes == 1
                            ? Strings.AMinute
                            : string.Format(CultureInfo.CurrentCulture, Strings.Minutes,
                                authenticationResult.LockTimeRemainingMinutes);

                    modelErrorMessage = string.Format(CultureInfo.CurrentCulture, Strings.UserAccountLocked, timeRemaining);
                }

                return SignInFailure(model, linkingAccount, modelErrorMessage);
            }

            var authenticatedUser = authenticationResult.AuthenticatedUser;
            bool usedMultiFactorAuthentication = false;
            if (linkingAccount)
            {
                // Verify account has no other external accounts
                if (authenticatedUser.User.Credentials.Any(c => c.IsExternal()) && !authenticatedUser.User.IsAdministrator)
                {
                    var message = string.Format(
                           CultureInfo.CurrentCulture,
                           Strings.AccountIsLinkedToAnotherExternalAccount,
                           authenticatedUser.User.EmailAddress);
                    return SignInFailure(model, linkingAccount, message);
                }

                // Link with an external account
                var loginUserDetails = await AssociateCredential(authenticatedUser);
                authenticatedUser = loginUserDetails?.AuthenticatedUser;
                if (authenticatedUser == null)
                {
                    return AuthenticationFailureOrExternalLinkExpired();
                }

                usedMultiFactorAuthentication = loginUserDetails.UsedMultiFactorAuthentication;
            }

            // If we are an administrator and Gallery.EnforcedAuthProviderForAdmin is set
            // to require a specific authentication provider, challenge that provider if needed.
            ActionResult challenge;
            if (ShouldChallengeEnforcedProvider(
                NuGetContext.Config.Current.EnforcedAuthProviderForAdmin, authenticatedUser, returnUrl, out challenge))
            {
                return challenge;
            }

            // If we are an administrator and Gallery.EnforcedTenantIdForAdmin is set
            // to require a specific tenant Id, check if the user logged in with the specified tenant.
            if (!SiteAdminHasValidTenant(authenticatedUser))
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.SiteAdminNotLoggedInWithRequiredTenant, NuGetContext.Config.Current.EnforcedTenantIdForAdmin);
                return SignInFailure(model, linkingAccount, errorMessage);
            }

            // Create session
            await _authService.CreateSessionAsync(OwinContext, authenticatedUser, usedMultiFactorAuthentication);

            TempData["ShowPasswordDeprecationWarning"] = true;
            return SafeRedirect(returnUrl);
        }

        private ActionResult SignInFailure(LogOnViewModel model, bool linkingAccount, string modelErrorMessage)
        {
            ModelState.AddModelError("SignIn", modelErrorMessage);

            return SignInOrExternalLinkView(model, linkingAccount);
        }

        internal bool ShouldChallengeEnforcedProvider(string enforcedProviders, AuthenticatedUser authenticatedUser, string returnUrl, out ActionResult challenge)
        {
            if (!string.IsNullOrEmpty(enforcedProviders)
                && authenticatedUser.CredentialUsed.Type != null
                && authenticatedUser.User.IsAdministrator)
            {
                // Seems we *need* a specific authentication provider. Check if we logged in using one...
                var providers = enforcedProviders.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                if (!providers.Any(p => string.Equals(p, authenticatedUser.CredentialUsed.Type, StringComparison.OrdinalIgnoreCase))
                    && !providers.Any(p => string.Equals(CredentialTypes.External.Prefix + p, authenticatedUser.CredentialUsed.Type, StringComparison.OrdinalIgnoreCase)))
                {
                    // Challenge authentication using the first required authentication provider
                    challenge = _authService.Challenge(
                        providers.First(),
                        Url.LinkExternalAccount(returnUrl));

                    return true;
                }
            }

            challenge = null;
            return false;
        }

        internal bool SiteAdminHasValidTenant(AuthenticatedUser authenticatedUser)
        {
            if (!string.IsNullOrEmpty(NuGetContext.Config.Current.EnforcedTenantIdForAdmin)
                && authenticatedUser.User.IsAdministrator)
            {
                return string.Equals(NuGetContext.Config.Current.EnforcedTenantIdForAdmin, authenticatedUser.CredentialUsed.TenantId, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        [HttpGet]
        public virtual ActionResult RegisterLegacy(string returnUrl)
        {
            return Redirect(Url.LogOnNuGetAccount(returnUrl, relativeUrl: false));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Register(LogOnViewModel model, string returnUrl, bool linkingAccount)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[GalleryConstants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                TempData["Message"] = Strings.AlreadyLoggedIn;
                return SafeRedirect(returnUrl);
            }

            if (linkingAccount)
            {
                ModelState.Remove("Register.Password");
            }

            if (!ModelState.IsValid)
            {
                return RegisterOrExternalLinkView(model, linkingAccount);
            }

            AuthenticatedUser user;
            var usedMultiFactorAuthentication = false;
            try
            {
                if (linkingAccount)
                {
                    var result = await _authService.ReadExternalLoginCredential(OwinContext);
                    if (result.ExternalIdentity == null)
                    {
                        return AuthenticationFailureOrExternalLinkExpired();
                    }

                    usedMultiFactorAuthentication = result.LoginDetails?.WasMultiFactorAuthenticated ?? false;
                    user = await _authService.Register(
                        model.Register.Username,
                        model.Register.EmailAddress,
                        result.Credential,
                        (result.Credential.IsExternal() && string.Equals(result.UserInfo?.Email, model.Register.EmailAddress))
                        );
                }
                else
                {
                    user = await _authService.Register(
                        model.Register.Username,
                        model.Register.EmailAddress,
                        _credentialBuilder.CreatePasswordCredential(model.Register.Password));
                }
            }
            catch (EntityException ex)
            {
                ModelState.AddModelError("Register", ex.Message);

                return RegisterOrExternalLinkView(model, linkingAccount);
            }

            // Send a new account email
            if (NuGetContext.Config.Current.ConfirmEmailAddresses && !string.IsNullOrEmpty(user.User.UnconfirmedEmailAddress))
            {
                var message = new NewAccountMessage(
                    _messageServiceConfiguration,
                    user.User,
                    Url.ConfirmEmail(
                        user.User.Username,
                        user.User.EmailConfirmationToken,
                        relativeUrl: false));

                await _messageService.SendMessageAsync(message);
            }

            // If we are an administrator and Gallery.EnforcedAuthProviderForAdmin is set
            // to require a specific authentication provider, challenge that provider if needed.
            ActionResult challenge;
            if (ShouldChallengeEnforcedProvider(
                NuGetContext.Config.Current.EnforcedAuthProviderForAdmin, user, returnUrl, out challenge))
            {
                return challenge;
            }

            // If we are an administrator and Gallery.EnforcedTenantIdForAdmin is set
            // to require a specific tenant Id, check if the user logged in with the specified tenant.
            if (!SiteAdminHasValidTenant(user))
            {
                ModelState.AddModelError("Register", string.Format(Strings.SiteAdminNotLoggedInWithRequiredTenant, NuGetContext.Config.Current.EnforcedTenantIdForAdmin));
                return RegisterOrExternalLinkView(model, linkingAccount);
            }

            // Create session
            await _authService.CreateSessionAsync(OwinContext, user, usedMultiFactorAuthentication);
            return RedirectFromRegister(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult LogOff(string returnUrl)
        {
            OwinContext.Authentication.SignOut();

            if (!string.IsNullOrEmpty(returnUrl)
                && (returnUrl.Contains("account") || returnUrl.Contains("Admin")))
            {
                returnUrl = null;
            }

            return SafeRedirect(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> SignInAssistance(string username, string providedEmailAddress)
        {
            // If provided email address is empty or null, return the result with a formatted
            // email address, otherwise send sign-in assistance email to the associated mail address.
            try
            {
                var user = _userService.FindByUsername(username);
                if (user == null)
                {
                    throw new ArgumentException(Strings.UserNotFound);
                }

                var email = user.EmailAddress ?? user.UnconfirmedEmailAddress;
                if (string.IsNullOrWhiteSpace(providedEmailAddress))
                {
                    var formattedEmail = FormatEmailAddressForAssistance(email);
                    return Json(new { success = true, EmailAddress = formattedEmail });
                }
                else
                {
                    if (!email.Equals(providedEmailAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException(Strings.SigninAssistance_EmailMismatched);
                    }
                    else
                    {
                        var message = new SigninAssistanceMessage(
                            _messageServiceConfiguration,
                            new MailAddress(email, user.Username),
                            user.Credentials.Where(cred => cred.IsExternal()));

                        await _messageService.SendMessageAsync(message);

                        return Json(new { success = true });
                    }
                }
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [ActionName("Authenticate")]
        [HttpGet]
        public virtual ActionResult AuthenticateGet(string returnUrl, string provider)
        {
            return AuthenticateAndLinkExternal(returnUrl, provider);
        }

        [ActionName("Authenticate")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult AuthenticatePost(string returnUrl, string provider)
        {
            return AuthenticateAndLinkExternal(returnUrl, provider);
        }

        [ActionName("AuthenticateExternal")]
        [HttpGet]
        public virtual ActionResult AuthenticateExternal(string returnUrl)
        {
            var user = GetCurrentUser();
            var userOrganizationsWithTenantPolicy = user?
                .Organizations?
                .Where(o => o
                    .Organization
                    .SecurityPolicies
                    .Any(policy => policy.Name == nameof(RequireOrganizationTenantPolicy)));

            if (userOrganizationsWithTenantPolicy != null && userOrganizationsWithTenantPolicy.Any())
            {
                var aadCredential = user?.Credentials.GetAzureActiveDirectoryCredential();
                if (aadCredential != null)
                {
                    var orgList = string.Join(", ",
                        userOrganizationsWithTenantPolicy.Select(member => member.Organization.Username));

                    TempData["WarningMessage"] = string.Format(Strings.ChangeCredential_NotAllowed, orgList);
                    return Redirect(returnUrl);
                }
            }

            var externalAuthProvider = GetExternalProvider();
            if (externalAuthProvider == null)
            {
                TempData["Message"] = Strings.ChangeCredential_ProviderNotFound;
                return Redirect(returnUrl);
            }

            return ChallengeAuthentication(Url.LinkOrChangeExternalCredential(returnUrl), externalAuthProvider);
        }

        [NonAction]
        public ActionResult AuthenticateAndLinkExternal(string returnUrl, string provider)
        {
            TempData["ShowPasswordDeprecationWarning"] = true;
            return ChallengeAuthentication(Url.LinkExternalAccount(returnUrl), provider);
        }

        [NonAction]
        public ActionResult ChallengeAuthentication(string returnUrl, string provider, AuthenticationPolicy policy = null)
        {
            try
            {
                return _authService.Challenge(provider, returnUrl, policy);
            }
            catch (InvalidOperationException)
            {
                // This exception will be thrown when the unsupported provider is invoked, return 404.
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }
        }

        /// <summary>
        /// This is the challenge response action for linking or replacing external credentials
        /// after external authentication.
        /// </summary>
        /// <param name="returnUrl">The url to return upon credential replacement</param>
        /// <returns><see cref="ActionResult"/> for returnUrl</returns>
        public virtual async Task<ActionResult> LinkOrChangeExternalCredential(string returnUrl)
        {
            var user = GetCurrentUser();
            var result = await _authService.ReadExternalLoginCredential(OwinContext);
            if (result?.Credential == null)
            {
                TempData["ErrorMessage"] = Strings.ExternalAccountLinkExpired;
                return SafeRedirect(returnUrl);
            }

            var newCredential = result.Credential;
            if (await _authService.TryReplaceCredential(user, newCredential))
            {
                // Remove the password credential after linking to external account.
                await RemovePasswordCredential(user);

                // Authenticate with the new credential after successful replacement
                var authenticatedUser = await _authService.Authenticate(newCredential);
                var usedMultiFactorAuthentication = result.LoginDetails?.WasMultiFactorAuthenticated ?? false;
                await _authService.CreateSessionAsync(OwinContext, authenticatedUser, usedMultiFactorAuthentication);

                // Get email address of the new credential for updating success message
                var newEmailAddress = GetEmailAddressFromExternalLoginResult(result, out string errorReason);
                if (!string.IsNullOrEmpty(errorReason))
                {
                    TempData["ErrorMessage"] = errorReason;
                    return SafeRedirect(returnUrl);
                }

                var linkingDifferentEmailAddress = !newEmailAddress.Equals(user.EmailAddress, StringComparison.OrdinalIgnoreCase);
                TempData["Message"] = linkingDifferentEmailAddress
                    ? string.Format(Strings.ChangeCredential_SuccessDifferentEmail, newEmailAddress, user.EmailAddress)
                    : string.Format(Strings.ChangeCredential_Success, newEmailAddress);
            }
            else
            {
                // The identity value contains cookie non-compliant characters like `<, >`(eg: John Doe <john@doe.com>), 
                // These need to be replaced so that they are not treated as HTML tags
                TempData["RawErrorMessage"] = string.Format(Strings.ChangeCredential_Failed,
                    newCredential.Identity.Replace("<", "&lt;").Replace(">", "&gt;"),
                    UriExtensions.GetExternalUrlAnchorTag("FAQs page", GalleryConstants.FAQLinks.MSALinkedToAnotherAccount));
            }

            return SafeRedirect(returnUrl);
        }

        public virtual async Task<ActionResult> LinkExternalAccount(string returnUrl, string error = null, string errorDescription = null)
        {
            // Extract the external login info
            var result = await _authService.AuthenticateExternalLogin(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                string errorMessage = GetAuthenticationFailureMessage(error, errorDescription);
                return AuthenticationFailureOrExternalLinkExpired(errorMessage);
            }

            if (result.Authentication != null)
            {
                // If we are an administrator and Gallery.EnforcedAuthProviderForAdmin is set
                // to require a specific authentication provider, challenge that provider if needed.
                ActionResult challenge;
                if (ShouldChallengeEnforcedProvider(
                    NuGetContext.Config.Current.EnforcedAuthProviderForAdmin, result.Authentication, returnUrl, out challenge))
                {
                    return challenge;
                }

                // If we are an administrator and Gallery.EnforcedTenantIdForAdmin is set
                // to require a specific tenant Id, check if the user logged in with the specified tenant.
                if (!SiteAdminHasValidTenant(result.Authentication))
                {
                    string errorMessage = string.Format(Strings.SiteAdminNotLoggedInWithRequiredTenant, NuGetContext.Config.Current.EnforcedTenantIdForAdmin);
                    return AuthenticationFailureOrExternalLinkExpired(errorMessage);
                }

                if (ShouldEnforceMultiFactorAuthentication(result))
                {
                    // Invoke the authentication again enforcing multi-factor authentication for the same provider.
                    return ChallengeAuthentication(
                        Url.LinkExternalAccount(returnUrl),
                        result.Authenticator.Name,
                        new AuthenticationPolicy() { Email = result.LoginDetails.EmailUsed, EnforceMultiFactorAuthentication = true });
                }

                // Remove the password login if the password logins are deprecated and enforced discontinuation.
                if (NuGetContext.Config.Current.DeprecateNuGetPasswordLogins
                    && _contentObjectService.LoginDiscontinuationConfiguration.IsPasswordLoginDiscontinuedForAll()
                    && result.Authentication.CredentialUsed.IsExternal()
                    && result.Authentication.User.HasPasswordCredential())
                {
                    // Remove password logins when a user signs in with an external login.
                    TempData["Message"] = string.Format(Strings.DiscontinuedLogin_PasswordRemoved, NuGetContext.Config.Current.Brand);
                    await RemovePasswordCredential(result.Authentication.User);
                }

                // Create session
                await _authService.CreateSessionAsync(OwinContext,
                    result.Authentication,
                    wasMultiFactorAuthenticated: result?.LoginDetails?.WasMultiFactorAuthenticated ?? false);

                // Update the 2FA if used during login but user does not have it set on their account. Enforced for only personal microsoft accounts
                // record it in the DB for the AAD accounts.
                if (result?.LoginDetails != null
                    && result.LoginDetails.WasMultiFactorAuthenticated
                    && !result.Authentication.User.EnableMultiFactorAuthentication
                    && CredentialTypes.IsExternal(result.Credential))
                {
                    await _userService.ChangeMultiFactorAuthentication(result.Authentication.User, enableMultiFactor: true, referrer: "Authentication");
                    OwinContext.AddClaim(NuGetClaims.EnabledMultiFactorAuthentication);
                    if (CredentialTypes.IsMicrosoftAccount(result.Credential.Type))
                    {
                        TempData["Message"] = Strings.MultiFactorAuth_LoginUpdate;
                    }
                }

                // Ask the user to enable multifactor authentication, if the user
                // 1. Does not have 2FA enabled, and
                // 2. Signed in with MSA account.
                var currentUser = result.Authentication.User;
                if (!currentUser.EnableMultiFactorAuthentication
                    && CredentialTypes.IsMicrosoftAccount(result.Credential.Type))
                {
                    TempData[GalleryConstants.AskUserToEnable2FA] = true;
                }

                return SafeRedirect(returnUrl);
            }
            else
            {
                // Gather data for view model
                string name = null;
                string email = null;
                var authUI = result.Authenticator.GetUI();
                try
                {
                    var userInfo = result.Authenticator.GetIdentityInformation(result.ExternalIdentity);
                    name = userInfo.Name;
                    email = userInfo.Email;
                }
                catch (Exception)
                {
                    // Consume the exception for now, for backwards compatibility to previous MSA provider.
                    email = result.ExternalIdentity.GetClaimOrDefault(ClaimTypes.Email);
                    name = result.ExternalIdentity.GetClaimOrDefault(ClaimTypes.Name);
                }

                // Check for a user with this email address
                User existingUser = null;
                if (!string.IsNullOrEmpty(email))
                {
                    existingUser = _userService.FindByEmailAddress(email);
                }

                var foundExistingUser = existingUser != null;
                var existingUserLinkingError = AssociateExternalAccountViewModel.ExistingUserLinkingErrorType.None;

                if (foundExistingUser)
                {
                    if (existingUser is Organization)
                    {
                        existingUserLinkingError = AssociateExternalAccountViewModel.ExistingUserLinkingErrorType.AccountIsOrganization;
                    }
                    else if (existingUser.Credentials.Any(c => c.IsExternal()) && !existingUser.IsAdministrator)
                    {
                        existingUserLinkingError = AssociateExternalAccountViewModel.ExistingUserLinkingErrorType.AccountIsAlreadyLinked;
                    }
                }

                var external = new AssociateExternalAccountViewModel()
                {
                    ProviderAccountNoun = authUI.AccountNoun,
                    AccountName = name,
                    FoundExistingUser = foundExistingUser,
                    ExistingUserLinkingError = existingUserLinkingError
                };

                var model = new LogOnViewModel
                {
                    External = external,
                    SignIn = new SignInViewModel
                    {
                        UserNameOrEmail = email
                    },
                    Register = new RegisterViewModel
                    {
                        EmailAddress = email
                    }
                };

                return LinkExternalView(model);
            }
        }

        internal bool ShouldEnforceMultiFactorAuthentication(AuthenticateExternalLoginResult result)
        {
            if (result?.Authenticator == null || result.Authentication == null)
            {
                return false;
            }

            // Enforce multi-factor authentication only if:
            // 1. The authenticator supports multi-factor authentication, otherwise no use.
            // 2. The user has enabled multi-factor authentication for their account.
            // 3. The user authenticated with the personal microsoft account. AAD 2FA policy is controlled by the tenant admins.
            // 4. The user did not use the multi-factor authentication for the session, obviously.
            return result.Authenticator.SupportsMultiFactorAuthentication()
                && result.Authentication.User.EnableMultiFactorAuthentication
                && !result.LoginDetails.WasMultiFactorAuthenticated
                && result.Authentication.CredentialUsed.IsExternal()
                && (CredentialTypes.IsMicrosoftAccount(result.Authentication.CredentialUsed.Type));
        }

        private string FormatEmailAddressForAssistance(string email)
        {
            var emailParts = email.Split('@');
            if (emailParts.Length != 2)
            {
                throw new ArgumentException(@"Invalid email address. Please contact support for assistance.");
            }

            // Format the email address as x**********y@domain.com
            // One character wide email id eg: x@domain.com; format it as x**********@domain.com
            var idPart = emailParts[0];
            var domainPart = emailParts[1];
            return idPart[0]
              + EMAIL_FORMAT_PADDING
              + (idPart.Length > 1 ? idPart[idPart.Length - 1] + "" : "")
              + $"@{domainPart}";
        }

        private ActionResult RedirectFromRegister(string returnUrl)
        {
            if (returnUrl != Url.Home())
            {
                // User was on their way to a page other than the home page. Redirect them with a thank you for registering message.
                TempData["Message"] = "Your account is now registered!";
                return SafeRedirect(returnUrl);
            }

            // User was not on their way anywhere in particular. Show them the thanks/welcome page.
            return RedirectToAction(actionName: "Thanks", controllerName: "Users");
        }

        private async Task<LoginUserDetails> AssociateCredential(AuthenticatedUser user)
        {
            var result = await _authService.ReadExternalLoginCredential(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                return null;
            }

            await _authService.AddCredential(user.User, result.Credential);

            await RemovePasswordCredential(user.User);

            // Notify the user of the change
            var emailMessage = new CredentialAddedMessage(
                _messageServiceConfiguration,
                user.User,
                _authService.DescribeCredential(result.Credential).GetCredentialTypeInfo());
            await _messageService.SendMessageAsync(emailMessage);

            return new LoginUserDetails
            {
                AuthenticatedUser = new AuthenticatedUser(user.User, result.Credential),
                UsedMultiFactorAuthentication = result.LoginDetails?.WasMultiFactorAuthenticated ?? false
            };
        }

        private async Task RemovePasswordCredential(User user)
        {
            var passwordCredential = user.GetPasswordCredential();
            if (passwordCredential != null)
            {
                await _authService.RemoveCredential(user, passwordCredential);
            }
        }

        private List<AuthenticationProviderViewModel> GetProviders()
        {
            return (from p in _authService.Authenticators.Values
                    where p.BaseConfig.Enabled
                    let ui = p.GetUI()
                    where ui != null && ui.ShowOnLoginPage
                    select new AuthenticationProviderViewModel()
                    {
                        ProviderName = p.Name,
                        UI = ui
                    }).ToList();
        }

        private string GetExternalProvider()
        {
            // Get list of all enabled providers
            var providers = GetProviders();

            // Select one provider to authenticate for linking when multiple external providers are enabled.
            // This is for backwards compatibility with MicrosoftAccount provider. 
            return ExternalAuthenticationPriority
                .FirstOrDefault(authenticator => providers.Any(p => p.ProviderName.Equals(authenticator, StringComparison.OrdinalIgnoreCase)));
        }

        private string GetEmailAddressFromExternalLoginResult(AuthenticateExternalLoginResult result, out string errorReason)
        {
            try
            {
                var userInformation = result.Authenticator?.GetIdentityInformation(result.ExternalIdentity);
                errorReason = null;
                return userInformation.Email;
            }
            catch (ArgumentException ex)
            {
                errorReason = ex.Message;
                return null;
            }
        }

        private ActionResult AuthenticationFailureOrExternalLinkExpired(string errorMessage = null)
        {
            // User got here without an external login cookie (or an expired one)
            // Send them to the logon action with a message
            TempData["ErrorMessage"] = string.IsNullOrEmpty(errorMessage) ? Strings.ExternalAccountLinkExpired : errorMessage;
            return Redirect(Url.LogOn(null, relativeUrl: false));
        }

        private ActionResult SignInOrExternalLinkView(LogOnViewModel model, bool linkingAccount)
        {
            if (linkingAccount)
            {
                return LinkExternalView(model);
            }
            else
            {
                return SignInNuGetAccountView(model);
            }
        }

        private ActionResult RegisterOrExternalLinkView(LogOnViewModel model, bool linkingAccount)
        {
            if (linkingAccount)
            {
                return LinkExternalView(model);
            }
            else
            {
                return RegisterView(model);
            }
        }

        private ActionResult LoggedInRedirect(string returnUrl)
        {
            TempData["Message"] = Strings.AlreadyLoggedIn;
            return SafeRedirect(returnUrl);
        }

        private ActionResult SignInView(LogOnViewModel existingModel)
        {
            return AuthenticationView("SignIn", existingModel);
        }

        private ActionResult RegisterView(LogOnViewModel existingModel)
        {
            return AuthenticationView("Register", existingModel);
        }

        private ActionResult SignInNuGetAccountView(LogOnViewModel existingModel)
        {
            return AuthenticationView("SignInNuGetAccount", existingModel);
        }

        private ActionResult LinkExternalView(LogOnViewModel existingModel)
        {
            return AuthenticationView("LinkExternal", existingModel);
        }

        private ActionResult AuthenticationView(string viewName, LogOnViewModel existingModel)
        {
            existingModel.Providers = GetProviders();
            existingModel.SignIn = existingModel.SignIn ?? new SignInViewModel();
            existingModel.Register = existingModel.Register ?? new RegisterViewModel();

            return View(viewName, existingModel);
        }

        private string GetAuthenticationFailureMessage(string error, string errorDescription)
        {
            if (string.IsNullOrEmpty(error))
            {
                return Strings.AuthenticationFailure_UnkownError;
            }

            switch (error)
            {
                case AuthenticationFailureErrors.ACCESSS_DENIED:
                case AuthenticationFailureErrors.CONSENT_REQUIRED:
                    return Strings.ExternalAccountLinkExpired;
                default:
                    return string.IsNullOrEmpty(errorDescription)
                        ? error
                        : errorDescription;
            }
        }
    }
}
