using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class AuthenticationController : Controller
    {
        public IFormsAuthenticationService FormsAuth { get; protected set; }
        public IUserService UserService { get; protected set; }
        
        // For sub-classes to initialize services themselves
        protected AuthenticationController()
        {
        }

        public AuthenticationController(
            IFormsAuthenticationService formsAuthService,
            IUserService userService)
        {
            FormsAuth = formsAuthService;
            UserService = userService;
        }

        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                TempData["Message"] = "You are already logged in!";
                return Redirect(returnUrl);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult SignIn(SignInRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You are already logged in!");
                return View();
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            var user = UserService.FindByUsernameOrEmailAddressAndPassword(
                request.UserNameOrEmail,
                request.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    Strings.UserNotFound);

                return View();
            }

            SetAuthenticationCookie(user);
            return SafeRedirect(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult Register(RegisterRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You are already logged in!");
                return View();
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            User user;
            try
            {
                user = UserService.Create(
                    request.Username,
                    request.Password,
                    request.EmailAddress);
            }
            catch (EntityException ex)
            {
                ModelState.AddModelError(String.Empty, ex.Message);
                return View();
            }

            SetAuthenticationCookie(user);

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
            // TODO: this should really be a POST

            FormsAuth.SignOut();

            return SafeRedirect(returnUrl);
        }

        [NonAction]
        protected virtual ActionResult SafeRedirect(string returnUrl)
        {
            return Redirect(RedirectHelper.SafeRedirectUrl(Url, returnUrl));
        }

        [NonAction]
        protected virtual void SetAuthenticationCookie(User user)
        {
            IEnumerable<string> roles = null;
            if (user.Roles.AnySafe())
            {
                roles = user.Roles.Select(r => r.Name);
            }

            FormsAuth.SetAuthCookie(
                user.Username,
                true,
                roles);
        }
    }
}
