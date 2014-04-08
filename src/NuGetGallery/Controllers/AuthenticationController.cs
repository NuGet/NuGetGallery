using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Diagnostics;
using NuGetGallery.Infrastructure;
using System.Net.Mail;

namespace NuGetGallery
{
    public partial class AuthenticationController : AppController
    {
        public AuthenticationService AuthService { get; protected set; }
        public IUserService UserService { get; protected set; }
        public IMessageService MessageService { get; protected set; }

        // For sub-classes to initialize services themselves
        protected AuthenticationController()
        {
        }

        public AuthenticationController(
            AuthenticationService authService,
            IUserService userService,
            IMessageService messageService)
        {
            AuthService = authService;
            UserService = userService;
            MessageService = messageService;
        }

        /// <summary>
        /// Sign In\Register view
        /// </summary>
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

            return LogOnView();
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
                return LogOnView(model);
            }

            var user = await AuthService.Authenticate(model.SignIn.UserNameOrEmail, model.SignIn.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    "SignIn",
                    Strings.UsernameAndPasswordNotFound);

                return LogOnView(model);
            }

            if (linkingAccount)
            {
                // Link with an external account
                user = await AssociateCredential(user);
                if (user == null)
                {
                    return ExternalLinkExpired();
                }
            }

            // Now log in!
            AuthService.CreateSession(OwinContext, user.User);
            return SafeRedirect(returnUrl);
        }

        [HttpPost]
        [RequireSsl]
        [ValidateAntiForgeryToken]
        public async virtual Task<ActionResult> Register(LogOnViewModel model, string returnUrl, bool linkingAccount)
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
                return LogOnView(model);
            }

            AuthenticatedUser user;
            try
            {

                if (linkingAccount)
                {
                    var result = await AuthService.ReadExternalLoginCredential(OwinContext);
                    if (result.ExternalIdentity == null)
                    {
                        return ExternalLinkExpired();
                    }

                    user = await AuthService.Register(
                        model.Register.Username,
                        model.Register.EmailAddress,
                        result.Credential);
                }
                else
                {
                    user = await AuthService.Register(
                        model.Register.Username,
                        model.Register.EmailAddress,
                        CredentialBuilder.CreatePbkdf2Password(model.Register.Password));
                }
            }
            catch (EntityException ex)
            {
                ModelState.AddModelError("Register", ex.Message);
                return LogOnView(model);
            }

            // Send a new account email
            if(NuGetContext.Config.Current.ConfirmEmailAddresses && !String.IsNullOrEmpty(user.User.UnconfirmedEmailAddress))
            {
                MessageService.SendNewAccountEmail(
                    new MailAddress(user.User.UnconfirmedEmailAddress, user.User.Username),
                    Url.ConfirmationUrl(
                        "Confirm",
                        "Users",
                        user.User.Username,
                        user.User.EmailConfirmationToken));
            }

            // We're logging in!
            AuthService.CreateSession(OwinContext, user.User);

            return RedirectFromRegister(returnUrl);
        }

        public virtual ActionResult LogOff(string returnUrl)
        {
            OwinContext.Authentication.SignOut();
            return SafeRedirect(returnUrl);
        }

        public virtual ActionResult Authenticate(string returnUrl, string provider)
        {
            return AuthService.Challenge(
                provider,
                Url.Action("LinkExternalAccount", "Authentication", new { ReturnUrl = returnUrl }));
        }

        public async virtual Task<ActionResult> LinkExternalAccount(string returnUrl)
        {
            // Extract the external login info
            var result = await AuthService.AuthenticateExternalLogin(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                return ExternalLinkExpired();
            }

            if (result.Authentication != null)
            {
                AuthService.CreateSession(OwinContext, result.Authentication.User);
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
                if (!String.IsNullOrEmpty(email))
                {
                    existingUser = UserService.FindByEmailAddress(email);
                }

                var external = new AssociateExternalAccountViewModel()
                {
                    ProviderAccountNoun = authUI.AccountNoun,
                    AccountName = name,
                    FoundExistingUser = existingUser != null
                };

                var model = new LogOnViewModel()
                {
                    External = external,
                    SignIn = new SignInViewModel()
                    {
                        UserNameOrEmail = email
                    },
                    Register = new RegisterViewModel()
                    {
                        EmailAddress = email
                    }
                };

                return LogOnView(model);
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
            var result = await AuthService.ReadExternalLoginCredential(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                return null;
            }

            await AuthService.AddCredential(user.User, result.Credential);

            // Notify the user of the change
            MessageService.SendCredentialAddedNotice(user.User, result.Credential);

            return new AuthenticatedUser(user.User, result.Credential);
        }

        private List<AuthenticationProviderViewModel> GetProviders()
        {
            return (from p in AuthService.Authenticators.Values
                    where p.BaseConfig.Enabled
                    let ui = p.GetUI()
                    where ui != null
                    select new AuthenticationProviderViewModel()
                    {
                        ProviderName = p.Name,
                        UI = ui
                    }).ToList();
        }

        private ActionResult LogOnView()
        {
            return LogOnView(new LogOnViewModel()
            {
                SignIn = new SignInViewModel(),
                Register = new RegisterViewModel()
            });
        }

        private ActionResult ExternalLinkExpired()
        {
            // User got here without an external login cookie (or an expired one)
            // Send them to the logon action with a message
            TempData["Message"] = Strings.ExternalAccountLinkExpired;
            return RedirectToAction("LogOn");
        }

        private ActionResult LogOnView(LogOnViewModel existingModel)
        {
            // Fill the providers list
            existingModel.Providers = GetProviders();

            // Reinitialize any nulled-out sub models
            existingModel.SignIn = existingModel.SignIn ?? new SignInViewModel();
            existingModel.Register = existingModel.Register ?? new RegisterViewModel();

            return View("LogOn", existingModel);
        }
    }
}
