using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGetGallery.Authentication;

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

        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult LogOn(string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
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

            if (Request.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You are already logged in!");
                return View();
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            var user = AuthService.Authenticate(request.UserNameOrEmail, request.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    String.Empty,
                    Strings.UsernameAndPasswordNotFound);

                return View();
            }

            AuthService.CreateSession(HttpContext.GetOwinContext(), user);
            return SafeRedirect(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRemoteHttps(OnlyWhenAuthenticated = false)]
        public virtual ActionResult Register(RegisterRequest request, string returnUrl)
        {
            // I think it should be obvious why we don't want the current URL to be the return URL here ;)
            ViewData[Constants.ReturnUrlViewDataKey] = returnUrl;

            if (Request.IsAuthenticated)
            {
                ModelState.AddModelError(String.Empty, "You are already logged in!");
                return View();
            }

            if (!ModelState.IsValid)
            {
                return View();
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
                return View();
            }

            AuthService.CreateSession(HttpContext.GetOwinContext(), user);

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

        [NonAction]
        protected virtual ActionResult SafeRedirect(string returnUrl)
        {
            return Redirect(RedirectHelper.SafeRedirectUrl(Url, returnUrl));
        }
    }
}
