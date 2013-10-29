using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.ViewModels;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace NuGetGallery
{
    public partial class AuthenticationController : AppController
    {
        public AuthenticationService AuthService { get; protected set; }
        
        // For sub-classes to initialize services themselves
        protected AuthenticationController()
        {
        }

        public AuthenticationController(
            AuthenticationService authService)
        {
            AuthService = authService;
        }

        [RequireSsl]
        public virtual ActionResult LogOn(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            // Collect Authentication Providers
            if (Request.IsAuthenticated)
            {
                TempData["Message"] = "You are already logged in!";
                return Redirect(returnUrl);
            }

            return LogOnView();
        }

        [HttpPost]
        [RequireSsl]
        [ValidateAntiForgeryToken]
        public virtual ActionResult SignIn(SignInRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You are already logged in!");
                return LogOnView(signIn: request);
            }

            if (!ModelState.IsValid)
            {
                return LogOnView(signIn: request);
            }

            var user = AuthService.Authenticate(request.UserNameOrEmail, request.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    Strings.UsernameAndPasswordNotFound);

                return LogOnView(signIn: request);
            }

            AuthService.CreateSession(OwinContext, user.User, AuthenticationTypes.LocalUser);
            return SafeRedirect(returnUrl);
        }

        [HttpPost]
        [RequireSsl]
        [ValidateAntiForgeryToken]
        public virtual ActionResult Register(RegisterRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You are already logged in!");
                return LogOnView(register: request);
            }

            if (!ModelState.IsValid)
            {
                return LogOnView(register: request);
            }

            AuthenticatedUser user;
            try
            {
                user = AuthService.Register(
                    request.Username,
                    request.Password,
                    request.EmailAddress);
            }
            catch (EntityException ex)
            {
                ModelState.AddModelError(String.Empty, ex.Message);
                return LogOnView(register: request);
            }

            AuthService.CreateSession(OwinContext, user.User, AuthenticationTypes.LocalUser);

            if (RedirectHelper.SafeRedirectUrl(Url, returnUrl) != RedirectHelper.SafeRedirectUrl(Url, null))
            {
                // User was on their way to a page other than the home page. Redirect them with a thank you for registering message.
                TempData["Message"] = "Your account is now registered!";
                return new RedirectResult(RedirectHelper.SafeRedirectUrl(Url, returnUrl));
            }

            // User was not on their way anywhere in particular. Show them the thanks/welcome page.
            return RedirectToAction(MVC.Users.Thanks());
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

        public async virtual Task<ActionResult> LinkExternalAccount(string returnUrl, string provider)
        {
            // Extract the external login info
            var result = await AuthService.AuthenticateExternalLogin(OwinContext);
            if (result.ExternalIdentity == null)
            {
                // User got here without an external login cookie (or an expired one)
                // Send them to the logon action
                return RedirectToAction("LogOn");
            }

            if (result.Authentication != null)
            {
                return Content("Authenticated: " + result.Authentication.User.Username);
            }
            else
            {
                // Gather data for register model
                var email = result.ExternalIdentity.GetClaimOrDefault(ClaimTypes.Email);
                var name = RegisterRequest.NormalizeUserName(result
                    .ExternalIdentity
                    .GetClaimOrDefault(ClaimTypes.Name));

                var register = new RegisterRequest()
                {
                    Username = name,
                    EmailAddress = email
                };

                return LogOnView(associatingExternalLogin: true, register: register);
            }
        }

        [NonAction]
        protected virtual ActionResult SafeRedirect(string returnUrl)
        {
            return Redirect(RedirectHelper.SafeRedirectUrl(Url, returnUrl));
        }

        private ViewResult LogOnView(SignInRequest signIn = null, RegisterRequest register = null, bool associatingExternalLogin = false) {
            signIn = signIn ?? new SignInRequest();
            register = register ?? (associatingExternalLogin ? new RegisterRequest() : new RegisterLocalUserRequest());

            register.ShowPassword = !associatingExternalLogin;
            signIn.Providers = GetProviders();

            return View("LogOn", new LogOnViewModel()
            {
                SignIn = signIn,
                Register = register,
                AssociatingExternalLogin = associatingExternalLogin
            });
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
    }
}
