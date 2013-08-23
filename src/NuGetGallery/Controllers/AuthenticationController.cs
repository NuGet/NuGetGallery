using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class AuthenticationController : Controller
    {
        public IFormsAuthenticationService FormsAuth { get; protected set; }
        public IUserService Users { get; protected set; }
        
        // For sub-classes to initialize services themselves
        protected AuthenticationController()
        {
        }

        public AuthenticationController(
            IFormsAuthenticationService formsAuthService,
            IUserService userService)
        {
            FormsAuth = formsAuthService;
            Users = userService;
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

            var user = Users.FindByUsernameOrEmailAddressAndPassword(
                request.UserNameOrEmail,
                request.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    Strings.UserNotFound);

                return View();
            }

            if (!user.Confirmed)
            {
                ViewBag.ConfirmationRequired = true;
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
    }
}
