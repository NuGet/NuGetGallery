using System.Web.Mvc;

namespace NuGetGallery {
    public class AuthenticationController : Controller {
        public const string Name = "Authentication";

        readonly IFormsAuthenticationService formsAuthSvc;
        readonly IUserService userSvc;

        public AuthenticationController(
            IFormsAuthenticationService formsAuthSvc,
            IUserService userSvc) {
            this.formsAuthSvc = formsAuthSvc;
            this.userSvc = userSvc;
        }

        public ActionResult LogOn() {
            return View();
        }

        [HttpPost]
        public ActionResult LogOn(SignInRequest request, string returnUrl) {
            // TODO: improve the styling of the validation summary
            // TODO: modify the Object.cshtml partial to make the first text box autofocus, or use additional metadata

            if (!ModelState.IsValid)
                return View();

            // TODO: allow users to sign in with email address in addition to user name
            var user = userSvc.FindByUsernameAndPassword(
                request.UserNameOrEmail,
                request.Password);

            if (user == null) {
                ModelState.AddModelError(
                    string.Empty,
                    Strings.UserNotFound);

                return View();
            }

            formsAuthSvc.SetAuthCookie(
                user.Username,
                true);

            return SafeRedirect(returnUrl);
        }

        public ActionResult LogOff(string returnUrl) {
            // TODO: this should really be a POST

            formsAuthSvc.SignOut();

            return SafeRedirect(returnUrl);
        }

        public virtual ActionResult SafeRedirect(string returnUrl) {
            if (!string.IsNullOrWhiteSpace(returnUrl)
                && Url.IsLocalUrl(returnUrl)
                && returnUrl.Length > 1
                && returnUrl.StartsWith("/")
                && !returnUrl.StartsWith("//")
                && !returnUrl.StartsWith("/\\")) {
                return Redirect(returnUrl);
            }
            else {
                return Redirect(Url.Home());
            }
        }
    }
}