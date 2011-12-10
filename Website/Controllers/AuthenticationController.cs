using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class AuthenticationController : Controller
    {
        private readonly IFormsAuthenticationService formsAuthSvc;
        private readonly IUserService userSvc;

        public AuthenticationController(
            IFormsAuthenticationService formsAuthSvc,
            IUserService userSvc)
        {
            this.formsAuthSvc = formsAuthSvc;
            this.userSvc = userSvc;
        }

        [RequireRemoteHttps]
        public virtual ActionResult LogOn()
        {
            return View();
        }

        [HttpPost, RequireRemoteHttps]
        public virtual ActionResult LogOn(SignInRequest request, string returnUrl)
        {
            // TODO: improve the styling of the validation summary
            // TODO: modify the Object.cshtml partial to make the first text box autofocus, or use additional metadata

            if (!ModelState.IsValid)
                return View();

            var user = userSvc.FindByUsernameOrEmailAddressAndPassword(
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

            formsAuthSvc.SetAuthCookie(
                user.Username,
                true,
                roles);

            return SafeRedirect(returnUrl);
        }

        public virtual ActionResult LogOff(string returnUrl)
        {
            // TODO: this should really be a POST

            formsAuthSvc.SignOut();

            return SafeRedirect(returnUrl);
        }

        [NonAction]
        public virtual ActionResult SafeRedirect(string returnUrl)
        {
            if (!String.IsNullOrWhiteSpace(returnUrl)
                && Url.IsLocalUrl(returnUrl)
                && returnUrl.Length > 1
                && returnUrl.StartsWith("/", StringComparison.Ordinal)
                && !returnUrl.StartsWith("//", StringComparison.Ordinal)
                && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return Redirect(Url.Home());
            }
        }
    }
}