using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Web.Mvc;
using NuGetGallery.Configuration;

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

            IEnumerable<string> roles = null;
            if (user.Roles.AnySafe())
            {
                roles = user.Roles.Select(r => r.Name);
            }

            FormsAuth.SetAuthCookie(
                user.Username,
                true,
                roles);

            return SafeRedirect(returnUrl);
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

        public virtual ActionResult Register(string returnUrl)
        {
            // We don't want Login to have us as a return URL. 
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult Register(RegisterRequest request)
        {
            // If we have to render a view, we don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = request.ReturnUrl;

            if (!ModelState.IsValid) // Validates username and email regexes etc
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

            // Set Authorization cookie. Make sure you do this last, after sign-up has in fact succeeded.
            // Roles == null should always be fine for a new user.
            FormsAuth.SetAuthCookie(user.Username, true, roles: null);

            return RedirectToAction("Thanks", new { returnUrl = request.ReturnUrl });
        }

        public virtual ActionResult Thanks(string returnUrl)
        {
            // If we have to render a view, we don't want Login to have us as a return URL
            // By having this value present in the dictionary BUT null, we don't put "returnUrl" on the Login link at all
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            // The job of this page is now EITHER to redirect you to where you really wanted to go...
            // OR to tell you that our auth cookies are not working for some reason.
            if (User == null || !User.Identity.IsAuthenticated || String.IsNullOrEmpty(User.Identity.Name))
            {
                return View();
            }

            return SafeRedirect(returnUrl);
        }
    }
}
