// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery
{
    public partial class AuthenticationController
        : AppController
    {
        private readonly AuthenticationService _authService;

        private readonly IUserService _userService;

        private readonly IMessageService _messageService;

        private readonly ICredentialBuilder _credentialBuilder;

        public AuthenticationController(
            AuthenticationService authService,
            IUserService userService,
            IMessageService messageService,
            ICredentialBuilder credentialBuilder)
        {
            if (authService == null)
            {
                throw new ArgumentNullException(nameof(authService));
            }

            if (userService == null)
            {
                throw new ArgumentNullException(nameof(userService));
            }

            if (messageService == null)
            {
                throw new ArgumentNullException(nameof(messageService));
            }

            if (credentialBuilder == null)
            {
                throw new ArgumentNullException(nameof(credentialBuilder));
            }

            _authService = authService;
            _userService = userService;
            _messageService = messageService;
            _credentialBuilder = credentialBuilder;
        }

        /// <summary>
        /// Sign In\Register view
        /// </summary>
        [HttpGet]
        [RequireSsl]
        public virtual ActionResult LogOn(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                TempData["Message"] = Strings.AlreadyLoggedIn;
                return SafeRedirect(returnUrl);
            }

            return SignInView(new LogOnViewModel());
        }

        /// <summary>
        /// Sign In\Register view
        /// </summary>
        [HttpGet]
        [RequireSsl]
        public virtual ActionResult SignUp(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                TempData["Message"] = Strings.AlreadyLoggedIn;
                return SafeRedirect(returnUrl);
            }

            return RegisterView(new LogOnViewModel());
        }

        [HttpPost]
        [RequireSsl]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> SignIn(LogOnViewModel model, string returnUrl, bool linkingAccount)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                TempData["Message"] = Strings.AlreadyLoggedIn;
                return SafeRedirect(returnUrl);
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

                ModelState.AddModelError("SignIn", modelErrorMessage);

                return SignInOrExternalLinkView(model, linkingAccount);
            }

            var user = authenticationResult.AuthenticatedUser;
            
            if (linkingAccount)
            {
                // Link with an external account
                user = await AssociateCredential(user);
                if (user == null)
                {
                    return ExternalLinkExpired();
                }
            }

            // If we are an administrator and Gallery.EnforcedAuthProviderForAdmin is set
            // to require a specific authentication provider, challenge that provider if needed.
            ActionResult challenge;
            if (ShouldChallengeEnforcedProvider(
                NuGetContext.Config.Current.EnforcedAuthProviderForAdmin, user, returnUrl, out challenge))
            {
                return challenge;
            }

            // Create session
            await _authService.CreateSessionAsync(OwinContext, user);
            return SafeRedirect(returnUrl);
        }

        internal bool ShouldChallengeEnforcedProvider(string enforcedProviders, AuthenticatedUser authenticatedUser, string returnUrl, out ActionResult challenge)
        {
            if (!string.IsNullOrEmpty(enforcedProviders)
                && authenticatedUser.CredentialUsed.Type != null
                && authenticatedUser.User.IsInRole(Constants.AdminRoleName))
            {
                // Seems we *need* a specific authentication provider. Check if we logged in using one...
                var providers = enforcedProviders.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

                if (!providers.Any(p => string.Equals(p, authenticatedUser.CredentialUsed.Type, StringComparison.OrdinalIgnoreCase))
                    && !providers.Any(p => string.Equals(CredentialTypes.ExternalPrefix + p, authenticatedUser.CredentialUsed.Type, StringComparison.OrdinalIgnoreCase)))
                {
                    // Challenge authentication using the first required authentication provider
                    challenge = _authService.Challenge(
                        providers.First(),
                        Url.Action("LinkExternalAccount", "Authentication", new { ReturnUrl = returnUrl }));

                    return true;
                }
            }

            challenge = null;
            return false;
        }

        [HttpGet]
        [RequireSsl]
        public virtual ActionResult RegisterLegacy(string returnUrl)
        {
            return RedirectToAction("LogOn", new { returnUrl });
        }
        
        [HttpPost]
        [RequireSsl]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Register(LogOnViewModel model, string returnUrl, bool linkingAccount)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

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
            try
            {
                if (linkingAccount)
                {
                    var result = await _authService.ReadExternalLoginCredential(OwinContext);
                    if (result.ExternalIdentity == null)
                    {
                        return ExternalLinkExpired();
                    }

                    user = await _authService.Register(
                        model.Register.Username,
                        model.Register.EmailAddress,
                        result.Credential);
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
                _messageService.SendNewAccountEmail(
                    new MailAddress(user.User.UnconfirmedEmailAddress, user.User.Username),
                    Url.ConfirmationUrl(
                        "Confirm",
                        "Users",
                        user.User.Username,
                        user.User.EmailConfirmationToken));
            }

            // If we are an administrator and Gallery.EnforcedAuthProviderForAdmin is set
            // to require a specific authentication provider, challenge that provider if needed.
            ActionResult challenge;
            if (ShouldChallengeEnforcedProvider(
                NuGetContext.Config.Current.EnforcedAuthProviderForAdmin, user, returnUrl, out challenge))
            {
                return challenge;
            }

            // Create session
            await _authService.CreateSessionAsync(OwinContext, user);
            return RedirectFromRegister(returnUrl);
        }

        [HttpGet]
        public virtual ActionResult LogOff(string returnUrl)
        {
            OwinContext.Authentication.SignOut();

            if (!string.IsNullOrEmpty(returnUrl) 
                && returnUrl.Contains("account"))
            {
                returnUrl = null;
            }

            return SafeRedirect(returnUrl);
        }

        [ActionName("Authenticate")]
        [HttpGet]
        public virtual ActionResult AuthenticateGet(string returnUrl, string provider)
        {
            return ChallengeAuthentication(returnUrl, provider);
        }

        [ActionName("Authenticate")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult AuthenticatePost(string returnUrl, string provider)
        {
            return ChallengeAuthentication(returnUrl, provider);
        }

        [NonAction]
        public ActionResult ChallengeAuthentication(string returnUrl, string provider)
        {
            return _authService.Challenge(
                provider,
                Url.Action("LinkExternalAccount", "Authentication", new { ReturnUrl = returnUrl }));
        }

        public virtual async Task<ActionResult> LinkExternalAccount(string returnUrl)
        {
            // Extract the external login info
            var result = await _authService.AuthenticateExternalLogin(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                return ExternalLinkExpired();
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

                // Create session
                await _authService.CreateSessionAsync(OwinContext, result.Authentication);
                return SafeRedirect(returnUrl);
            }
            else
            {
                // Gather data for view model
                var authUI = result.Authenticator.GetUI();
                var email = result.ExternalIdentity.GetClaimOrDefault(ClaimTypes.Email);
                var name = result
                    .ExternalIdentity
                    .GetClaimOrDefault(ClaimTypes.Name);

                // Check for a user with this email address
                User existingUser = null;
                if (!string.IsNullOrEmpty(email))
                {
                    existingUser = _userService.FindByEmailAddress(email);
                }

                var external = new AssociateExternalAccountViewModel()
                {
                    ProviderAccountNoun = authUI.AccountNoun,
                    AccountName = name,
                    FoundExistingUser = existingUser != null
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

        private async Task<AuthenticatedUser> AssociateCredential(AuthenticatedUser user)
        {
            var result = await _authService.ReadExternalLoginCredential(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                return null;
            }

            await _authService.AddCredential(user.User, result.Credential);

            // Notify the user of the change
            _messageService.SendCredentialAddedNotice(user.User, result.Credential);

            return new AuthenticatedUser(user.User, result.Credential);
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

        private ActionResult ExternalLinkExpired()
        {
            // User got here without an external login cookie (or an expired one)
            // Send them to the logon action with a message
            TempData["Message"] = Strings.ExternalAccountLinkExpired;
            return RedirectToAction("LogOn");
        }

        private ActionResult SignInOrExternalLinkView(LogOnViewModel model, bool linkingAccount)
        {
            if (linkingAccount)
            {
                return LinkExternalView(model);
            }
            else
            {
                return SignInView(model);
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

        private ActionResult SignInView(LogOnViewModel existingModel)
        {
            return AuthenticationView("SignIn", existingModel);
        }

        private ActionResult RegisterView(LogOnViewModel existingModel)
        {
            return AuthenticationView("Register", existingModel);
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
    }
}
