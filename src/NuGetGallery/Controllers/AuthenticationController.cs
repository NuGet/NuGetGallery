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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(SignInRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            // TODO: improve the styling of the validation summary
            // TODO: modify the Object.cshtml partial to make the first text box autofocus, or use additional metadata

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

        public virtual ActionResult LogOff(string returnUrl)
        {
            // TODO: this should really be a POST

            FormsAuth.SignOut();

            return SafeRedirect(returnUrl);
        }

        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You cannot register because you are already logged in!");
                return View();
            }

            // We don't want Login to have us as a return URL. 
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult Register(RegisterRequest request)
        {
            if (User.Identity.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You cannot register because you are already logged in!");
                return View();
            }

            // If we have to render a view, we don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = null;

            // TODO: consider client-side validation for unique username
            // TODO: add email validation

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

            return RedirectToAction(MVC.Users.Thanks());
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
