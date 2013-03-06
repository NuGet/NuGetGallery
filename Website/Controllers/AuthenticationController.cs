using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using NuGetGallery.Infrastructure;
using WorldDomination.Web.Authentication;
using WorldDomination.Web.Authentication.Mvc;

namespace NuGetGallery
{
    public partial class AuthenticationController : Controller
    {
        private readonly IFormsAuthenticationService _formsAuthService;
        private readonly IAuthenticationService _oauth;
        private readonly IAuthenticationCallbackProvider _callback;
        private readonly IUserService _userService;

        public AuthenticationController(
            IFormsAuthenticationService formsAuthService,
            IAuthenticationService oauth,
            IAuthenticationCallbackProvider callback,
            IUserService userService)
        {
            _formsAuthService = formsAuthService;
            _oauth = oauth;
            _callback = callback;
            _userService = userService;
        }

        //[RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        //public virtual ActionResult RedirectToProvider(string providerName, string returnUrl)
        //{
        //    if (String.IsNullOrEmpty(providerName))
        //    {
        //        throw new ArgumentException("'providerName' must be a non-empty string", "providerName");
        //    }

        //    var landingPath = Url.Action(MVC.Authentication.ReturnFromOAuth(providerName, null));
        //    var providerSettings = _oauth.GetAuthenticateServiceSettings(providerName, Request.Url, landingPath);

        //    // Generate anti forgery token
        //    string oldCookie = Request.GetAntiForgeryCookie();
        //    string newCookie;
        //    string csrfToken;
        //    AntiForgery.GetTokens(oldCookie, out newCookie, out csrfToken);
        //    newCookie = newCookie ?? oldCookie; // If old cookie was still valid, new cookie will be null.

        //    // Build the state string
        //    string state = String.Concat(returnUrl, "|", csrfToken);

        //    // Build the callback url
        //    providerSettings.State = state;

        //    // Set the anti forgery token
        //    Response.SetAntiForgeryCookie(newCookie);

        //    var uri = _oauth.RedirectToAuthenticationProvider(providerSettings);

        //    return Redirect(uri.AbsoluteUri);
        //}

        //public virtual ActionResult ReturnFromOAuth(string providerName, string state)
        //{
        //    // Parse the state string
        //    string[] parsed = state.Split('|');
        //    if (parsed.Length != 2)
        //    {
        //        throw new InvalidOperationException("Invalid state data from OAuth provider");
        //    }
        //    string returnUrl = parsed[0];
        //    string csrf = parsed[1];

        //    // Validate the Anti-Forgery Token
        //    AntiForgery.Validate(Request.GetAntiForgeryCookie(), csrf);

        //    // Get settings
        //    var settings = _oauth.GetAuthenticateServiceSettings(providerName, Request.Url);
        //    settings.State = state; // We already did CSRF checking... just force the state check to work

        //    var model = new AuthenticateCallbackData();

        //    try
        //    {
        //        model.AuthenticatedClient = _oauth.GetAuthenticatedClient(settings, Request.QueryString);
        //    }
        //    catch (Exception ex)
        //    {
        //        model.Exception = ex;
        //    }

        //    // Stash the return URL in context, for now.
        //    HttpContext.Items["ReturnUrl"] = returnUrl;
        //    return _callback.Process(HttpContext, model);
        //}

        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(string returnUrl)
        {
            return View(new SignInRequest() { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(SignInRequest request, string returnUrl)
        {
            // TODO: improve the styling of the validation summary
            // TODO: modify the Object.cshtml partial to make the first text box autofocus, or use additional metadata

            if (!ModelState.IsValid)
            {
                return View();
            }

            var user = _userService.FindByUsernameOrEmailAddressAndPassword(
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

            _formsAuthService.SetAuthCookie(
                user.Username,
                true,
                roles);

            return SafeRedirect(returnUrl);
        }

        public virtual ActionResult LogOff(string returnUrl)
        {
            // TODO: this should really be a POST

            _formsAuthService.SignOut();

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

            return Redirect(Url.Home());
        }
    }
}